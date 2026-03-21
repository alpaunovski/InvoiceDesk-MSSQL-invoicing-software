# LocalDB payload

Download the SQL Server Express LocalDB installer (SqlLocalDB.msi, e.g., SQL Server 2022 Express LocalDB) from the official Microsoft download page and place it in this folder with the exact name `SqlLocalDB.msi`. The Inno Setup script bundles it and runs a silent install with `IACCEPTSQLLOCALDBLICENSETERMS=YES` when LocalDB is missing.
