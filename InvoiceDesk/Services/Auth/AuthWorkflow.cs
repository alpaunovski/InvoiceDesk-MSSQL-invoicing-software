using System;
using System.Threading.Tasks;
using System.Windows;
using InvoiceDesk.Models;
using InvoiceDesk.ViewModels;
using InvoiceDesk.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services.Auth;

/// <summary>
/// Orchestrates token validation and login prompts before the main UI is shown.
/// </summary>
public sealed class AuthWorkflow
{
    private readonly IAuthService _authService;
    private readonly ITokenStore _tokenStore;
    private readonly IServiceProvider _services;
    private readonly ILogger<AuthWorkflow> _logger;

    public AuthWorkflow(IAuthService authService, ITokenStore tokenStore, IServiceProvider services, ILogger<AuthWorkflow> logger)
    {
        _authService = authService;
        _tokenStore = tokenStore;
        _services = services;
        _logger = logger;
    }

    public async Task<bool> EnsureAuthenticatedAsync()
    {
        var cached = await _tokenStore.GetAsync();
        if (cached != null && !IsExpired(cached))
        {
            var validate = await _authService.ValidateTokenAsync(cached.Token);
            if (validate?.Valid == true)
            {
                _logger.LogInformation("Existing token validated for user {UserId}", validate.UserId);
                return true;
            }

            _logger.LogInformation("Cached token invalid; clearing");
            await _tokenStore.ClearAsync();
        }

        return await PromptLoginAsync();
    }

    private static bool IsExpired(SessionToken token)
    {
        return token.ExpiresAtUtc.HasValue && token.ExpiresAtUtc.Value <= DateTime.UtcNow;
    }

    private async Task<bool> PromptLoginAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var window = _services.GetRequiredService<LoginWindow>();
        if (window.DataContext is not LoginViewModel viewModel)
        {
            throw new InvalidOperationException("LoginWindow must be constructed with LoginViewModel");
        }

        viewModel.DeviceName = Environment.MachineName;

        viewModel.LoginSucceeded += async (_, result) =>
        {
            var envelope = new SessionToken
            {
                Token = result.Token,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, result.ExpiresIn)),
            };
            await _tokenStore.SaveAsync(envelope);
            _logger.LogInformation("Token stored after login");
            tcs.TrySetResult(true);
            window.DialogResult = true;
            window.Close();
        };

        viewModel.LoginFailed += (_, message) =>
        {
            _logger.LogWarning("Login failed: {Message}", message);
        };

        var result = window.ShowDialog();
        if (result != true)
        {
            tcs.TrySetResult(false);
        }

        return await tcs.Task.ConfigureAwait(false);
    }
}
