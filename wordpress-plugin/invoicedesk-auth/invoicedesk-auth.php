<?php
/**
 * Plugin Name: InvoiceDesk Auth
 * Description: Authentication, licensing, and session management for the InvoiceDesk desktop client.
 * Version: 1.0.0
 * Author: InvoiceDesk
 */

if (!defined('ABSPATH')) {
    exit;
}

class InvoiceDeskAuthPlugin
{
    private static $instance;
    private $table_name;

    public static function init()
    {
        if (self::$instance === null) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    private function __construct()
    {
        global $wpdb;
        $this->table_name = $wpdb->prefix . 'invoicedesk_sessions';

        add_action('rest_api_init', [$this, 'register_rest_routes']);
        add_action('admin_menu', [$this, 'register_admin_menu']);

        add_action('admin_post_invoicedesk_update_user', [$this, 'handle_user_update']);
        add_action('admin_post_invoicedesk_revoke_session', [$this, 'handle_revoke_session']);
        add_action('admin_post_invoicedesk_reset_password', [$this, 'handle_reset_password']);
    }

    public static function activate()
    {
        global $wpdb;
        $table = $wpdb->prefix . 'invoicedesk_sessions';
        $charset_collate = $wpdb->get_charset_collate();

        require_once ABSPATH . 'wp-admin/includes/upgrade.php';
        $sql = "CREATE TABLE {$table} (
            id INT UNSIGNED NOT NULL AUTO_INCREMENT,
            user_id BIGINT(20) UNSIGNED NOT NULL,
            token VARCHAR(255) NOT NULL,
            device_name VARCHAR(255) NULL,
            ip_address VARCHAR(45) NULL,
            created_at DATETIME NOT NULL,
            last_seen DATETIME NOT NULL,
            is_active TINYINT(1) NOT NULL DEFAULT 1,
            expires_at DATETIME NOT NULL,
            PRIMARY KEY  (id),
            KEY user_idx (user_id),
            KEY token_idx (token)
        ) {$charset_collate};";
        dbDelta($sql);

        // Seed default user meta where missing.
        $users = get_users(['fields' => ['ID']]);
        foreach ($users as $user) {
            if (get_user_meta($user->ID, 'max_sessions', true) === '') {
                update_user_meta($user->ID, 'max_sessions', 1);
            }
            if (get_user_meta($user->ID, 'account_status', true) === '') {
                update_user_meta($user->ID, 'account_status', 'active');
            }
        }
    }

    public function register_rest_routes()
    {
        register_rest_route('invoicedesk/v1', '/login', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_login'],
            'permission_callback' => '__return_true',
            'args' => [
                'email' => ['required' => true, 'sanitize_callback' => 'sanitize_email'],
                'password' => ['required' => true],
                'device_name' => ['required' => false, 'sanitize_callback' => 'sanitize_text_field'],
            ],
        ]);

        register_rest_route('invoicedesk/v1', '/validate', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_validate'],
            'permission_callback' => '__return_true',
        ]);

        register_rest_route('invoicedesk/v1', '/logout', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_logout'],
            'permission_callback' => '__return_true',
        ]);

        register_rest_route('invoicedesk/v1', '/sessions/list', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_admin_list_sessions'],
            'permission_callback' => function () {
                return current_user_can('manage_options');
            },
        ]);

        register_rest_route('invoicedesk/v1', '/sessions/revoke', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_admin_revoke_session'],
            'permission_callback' => function () {
                return current_user_can('manage_options');
            },
            'args' => [
                'session_id' => ['required' => true, 'sanitize_callback' => 'absint'],
            ],
        ]);
    }

    private function require_https()
    {
        if (!is_ssl()) {
            return new WP_Error('insecure_request', 'HTTPS is required', ['status' => 403]);
        }
        return true;
    }

    private function get_bearer_token(WP_REST_Request $request)
    {
        $header = $request->get_header('authorization');
        if (!$header) {
            $header = $request->get_header('Authorization');
        }
        if ($header && stripos($header, 'Bearer ') === 0) {
            return sanitize_text_field(substr($header, 7));
        }
        $token = $request->get_param('token');
        return $token ? sanitize_text_field($token) : '';
    }

    public function handle_login(WP_REST_Request $request)
    {
        $secure = $this->require_https();
        if (is_wp_error($secure)) {
            return $secure;
        }

        $email = sanitize_email($request->get_param('email'));
        $password = $request->get_param('password');
        $device_name = sanitize_text_field($request->get_param('device_name'));

        if (empty($email) || empty($password)) {
            return new WP_Error('invalid_request', 'Email and password are required', ['status' => 400]);
        }

        $user = get_user_by('email', $email);
        if (!$user || !wp_check_password($password, $user->user_pass, $user->ID)) {
            return new WP_Error('invalid_credentials', 'Invalid email or password', ['status' => 401]);
        }

        $status = get_user_meta($user->ID, 'account_status', true) ?: 'active';
        if ($status !== 'active') {
            return new WP_Error('account_suspended', 'Account is suspended', ['status' => 403]);
        }

        $max_sessions = (int) (get_user_meta($user->ID, 'max_sessions', true) ?: 1);
        $active_sessions = $this->get_active_sessions($user->ID);
        if (count($active_sessions) >= $max_sessions) {
            // Remove the oldest session to make space for the new one.
            $oldest = $active_sessions[0];
            global $wpdb;
            $wpdb->delete($this->table_name, ['id' => $oldest->id], ['%d']);
        }

        $token = bin2hex(random_bytes(32));
        $now = gmdate('Y-m-d H:i:s');
        $expires_at = gmdate('Y-m-d H:i:s', time() + DAY_IN_SECONDS);
        $ip = $this->get_ip_address();

        global $wpdb;
        $wpdb->insert(
            $this->table_name,
            [
                'user_id' => $user->ID,
                'token' => $token,
                'device_name' => $device_name,
                'ip_address' => $ip,
                'created_at' => $now,
                'last_seen' => $now,
                'is_active' => 1,
                'expires_at' => $expires_at,
            ],
            ['%d', '%s', '%s', '%s', '%s', '%s', '%d', '%s']
        );

        return new WP_REST_Response([
            'success' => true,
            'token' => $token,
            'expires_in' => DAY_IN_SECONDS,
        ]);
    }

    public function handle_validate(WP_REST_Request $request)
    {
        $secure = $this->require_https();
        if (is_wp_error($secure)) {
            return $secure;
        }

        $token = $this->get_bearer_token($request);
        if (empty($token)) {
            return new WP_Error('missing_token', 'Token is required', ['status' => 400]);
        }

        $session = $this->find_active_session($token);
        if (!$session) {
            return new WP_REST_Response(['valid' => false], 401);
        }

        $now = gmdate('Y-m-d H:i:s');
        if (strtotime($session->expires_at) <= time()) {
            $this->deactivate_session($session->id);
            return new WP_REST_Response(['valid' => false], 401);
        }

        global $wpdb;
        $wpdb->update(
            $this->table_name,
            [
                'last_seen' => $now,
                'ip_address' => $this->get_ip_address(),
            ],
            ['id' => $session->id],
            ['%s', '%s'],
            ['%d']
        );

        return new WP_REST_Response([
            'valid' => true,
            'user_id' => (int) $session->user_id,
        ]);
    }

    public function handle_logout(WP_REST_Request $request)
    {
        $secure = $this->require_https();
        if (is_wp_error($secure)) {
            return $secure;
        }

        $token = $this->get_bearer_token($request);
        if (empty($token)) {
            return new WP_Error('missing_token', 'Token is required', ['status' => 400]);
        }

        $session = $this->find_active_session($token);
        if ($session) {
            $this->deactivate_session($session->id);
        }

        return new WP_REST_Response(['success' => true]);
    }

    public function handle_admin_list_sessions(WP_REST_Request $request)
    {
        if (!$this->verify_rest_nonce($request)) {
            return new WP_Error('rest_forbidden', 'Invalid nonce', ['status' => 403]);
        }

        global $wpdb;
        $results = $wpdb->get_results(
            "SELECT s.id, u.user_email, s.device_name, s.ip_address, s.last_seen, s.created_at
             FROM {$this->table_name} s
             JOIN {$wpdb->users} u ON s.user_id = u.ID
             WHERE s.is_active = 1"
        );

        $list = array_map(function ($row) {
            return [
                'id' => (int) $row->id,
                'user_email' => $row->user_email,
                'device_name' => $row->device_name,
                'ip_address' => $row->ip_address,
                'last_seen' => $row->last_seen,
                'created_at' => $row->created_at,
            ];
        }, $results);

        return new WP_REST_Response($list);
    }

    public function handle_admin_revoke_session(WP_REST_Request $request)
    {
        if (!$this->verify_rest_nonce($request)) {
            return new WP_Error('rest_forbidden', 'Invalid nonce', ['status' => 403]);
        }

        $session_id = absint($request->get_param('session_id'));
        if (!$session_id) {
            return new WP_Error('invalid_session', 'Session id required', ['status' => 400]);
        }

        $this->deactivate_session($session_id);
        return new WP_REST_Response(['success' => true]);
    }

    private function get_active_sessions($user_id)
    {
        global $wpdb;
        $sql = $wpdb->prepare(
            "SELECT * FROM {$this->table_name} WHERE user_id = %d AND is_active = 1 ORDER BY created_at ASC",
            $user_id
        );
        return $wpdb->get_results($sql);
    }

    private function find_active_session($token)
    {
        global $wpdb;
        $sql = $wpdb->prepare(
            "SELECT * FROM {$this->table_name} WHERE token = %s AND is_active = 1 LIMIT 1",
            $token
        );
        return $wpdb->get_row($sql);
    }

    private function deactivate_session($session_id)
    {
        global $wpdb;
        $wpdb->update(
            $this->table_name,
            ['is_active' => 0],
            ['id' => $session_id],
            ['%d'],
            ['%d']
        );
    }

    private function verify_rest_nonce(WP_REST_Request $request)
    {
        $nonce = $request->get_header('X-WP-Nonce');
        if (!$nonce) {
            $nonce = $request->get_param('_wpnonce');
        }
        return $nonce && wp_verify_nonce($nonce, 'wp_rest');
    }

    private function get_ip_address()
    {
        $ip = $_SERVER['REMOTE_ADDR'] ?? '';
        return sanitize_text_field($ip);
    }

    public function register_admin_menu()
    {
        add_menu_page(
            'InvoiceDesk',
            'InvoiceDesk',
            'manage_options',
            'invoicedesk',
            [$this, 'render_users_page'],
            'dashicons-lock'
        );

        add_submenu_page(
            'invoicedesk',
            'Users',
            'Users',
            'manage_options',
            'invoicedesk',
            [$this, 'render_users_page']
        );

        add_submenu_page(
            'invoicedesk',
            'Sessions',
            'Sessions',
            'manage_options',
            'invoicedesk-sessions',
            [$this, 'render_sessions_page']
        );
    }

    public function render_users_page()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Unauthorized');
        }

        $users = get_users(['orderby' => 'user_email', 'order' => 'ASC']);
        $nonce = wp_create_nonce('invoicedesk_users');
        ?>
        <div class="wrap">
            <h1>InvoiceDesk Users</h1>
            <table class="widefat fixed striped">
                <thead>
                    <tr>
                        <th>Email</th>
                        <th>Max Sessions</th>
                        <th>Account Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                <?php foreach ($users as $user): 
                    $max_sessions = (int) (get_user_meta($user->ID, 'max_sessions', true) ?: 1);
                    $status = get_user_meta($user->ID, 'account_status', true) ?: 'active';
                ?>
                    <tr>
                        <td><?php echo esc_html($user->user_email); ?></td>
                        <td><?php echo esc_html($max_sessions); ?></td>
                        <td><?php echo esc_html($status); ?></td>
                        <td>
                            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>" style="display:inline-block;margin-right:6px;">
                                <input type="hidden" name="action" value="invoicedesk_update_user" />
                                <input type="hidden" name="_wpnonce" value="<?php echo esc_attr($nonce); ?>" />
                                <input type="hidden" name="user_id" value="<?php echo esc_attr($user->ID); ?>" />
                                <label>Max sessions: <input type="number" min="1" name="max_sessions" value="<?php echo esc_attr($max_sessions); ?>" /></label>
                                <label>Status:
                                    <select name="account_status">
                                        <option value="active" <?php selected($status, 'active'); ?>>Active</option>
                                        <option value="suspended" <?php selected($status, 'suspended'); ?>>Suspended</option>
                                    </select>
                                </label>
                                <button class="button button-primary" type="submit">Save</button>
                            </form>

                            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>" style="display:inline-block;">
                                <input type="hidden" name="action" value="invoicedesk_reset_password" />
                                <input type="hidden" name="_wpnonce" value="<?php echo esc_attr($nonce); ?>" />
                                <input type="hidden" name="user_id" value="<?php echo esc_attr($user->ID); ?>" />
                                <button class="button" type="submit" onclick="return confirm('Reset password for <?php echo esc_js($user->user_email); ?>?')">Reset Password</button>
                            </form>
                        </td>
                    </tr>
                <?php endforeach; ?>
                </tbody>
            </table>
        </div>
        <?php
    }

    public function render_sessions_page()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Unauthorized');
        }

        global $wpdb;
        $nonce = wp_create_nonce('invoicedesk_sessions');
        $sessions = $wpdb->get_results(
            "SELECT s.id, u.user_email, s.device_name, s.ip_address, s.created_at, s.last_seen
             FROM {$this->table_name} s
             JOIN {$wpdb->users} u ON s.user_id = u.ID
             WHERE s.is_active = 1
             ORDER BY s.last_seen DESC"
        );
        ?>
        <div class="wrap">
            <h1>Active Sessions</h1>
            <table class="widefat fixed striped">
                <thead>
                    <tr>
                        <th>User</th>
                        <th>Device</th>
                        <th>IP</th>
                        <th>Created</th>
                        <th>Last Seen</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                <?php foreach ($sessions as $session): ?>
                    <tr>
                        <td><?php echo esc_html($session->user_email); ?></td>
                        <td><?php echo esc_html($session->device_name); ?></td>
                        <td><?php echo esc_html($session->ip_address); ?></td>
                        <td><?php echo esc_html($session->created_at); ?></td>
                        <td><?php echo esc_html($session->last_seen); ?></td>
                        <td>
                            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>">
                                <input type="hidden" name="action" value="invoicedesk_revoke_session" />
                                <input type="hidden" name="_wpnonce" value="<?php echo esc_attr($nonce); ?>" />
                                <input type="hidden" name="session_id" value="<?php echo esc_attr($session->id); ?>" />
                                <button class="button" type="submit">Revoke</button>
                            </form>
                        </td>
                    </tr>
                <?php endforeach; ?>
                </tbody>
            </table>
        </div>
        <?php
    }

    public function handle_user_update()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Unauthorized');
        }
        check_admin_referer('invoicedesk_users');

        $user_id = absint($_POST['user_id'] ?? 0);
        $max_sessions = absint($_POST['max_sessions'] ?? 1);
        $status = sanitize_text_field($_POST['account_status'] ?? 'active');

        if ($user_id) {
            update_user_meta($user_id, 'max_sessions', max(1, $max_sessions));
            update_user_meta($user_id, 'account_status', $status === 'suspended' ? 'suspended' : 'active');
        }

        wp_safe_redirect(admin_url('admin.php?page=invoicedesk&updated=1'));
        exit;
    }

    public function handle_reset_password()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Unauthorized');
        }
        check_admin_referer('invoicedesk_users');

        $user_id = absint($_POST['user_id'] ?? 0);
        if ($user_id) {
            $new_password = wp_generate_password(16, true, true);
            wp_set_password($new_password, $user_id);
            wp_mail(get_userdata($user_id)->user_email, 'InvoiceDesk password reset', 'Your new password: ' . $new_password);
        }

        wp_safe_redirect(admin_url('admin.php?page=invoicedesk&password_reset=1'));
        exit;
    }

    public function handle_revoke_session()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Unauthorized');
        }
        check_admin_referer('invoicedesk_sessions');

        $session_id = absint($_POST['session_id'] ?? 0);
        if ($session_id) {
            $this->deactivate_session($session_id);
        }

        wp_safe_redirect(admin_url('admin.php?page=invoicedesk-sessions&revoked=1'));
        exit;
    }
}
register_activation_hook(__FILE__, ['InvoiceDeskAuthPlugin', 'activate']);

add_action('plugins_loaded', function () {
    InvoiceDeskAuthPlugin::init();
});
