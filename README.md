# LiveSync

LiveSync is a tool designed to synchronize specific file types across multiple locations, including local folders, Windows file shares, and FTP servers.

## Configuration

The configuration for LiveSync is stored in the `appsettings.json` file. Below is an example configuration:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    },
    "SyncConfiguration": {
        "SyncSettings": [
            {
                "Name": "Documents Sync",
                "FileExtensions": [".docx", ".pdf"],
                "Locations": [
                    {
                        "Path": "C:\\Work\\Shared\\Documents",
                        "Type": "Local"
                    },
                    {
                        "Path": "\\\\fileserver\\shared\\documents",
                        "Type": "FileShare",
                        "Username": "user",
                        "Password": "password"
                    },
                    {
                        "Path": "/remote/documents",
                        "Type": "Ftp",
                        "Username": "ftpuser",
                        "Password": "ftppassword",
                        "FtpHost": "ftp.example.com",
                        "FtpPort": 21,
                        "FtpHostTimezone": 0
                    }
                ]
            }
        ]
    }
}
```

### Logging

The `Logging` section configures the logging level for the application. You can adjust the log levels as needed.

### SyncConfiguration

The `SyncConfiguration` section contains the settings for file synchronization.

- **SyncSettings**: A list of synchronization settings.
  - **Name**: The name of the synchronization setting.
  - **FileExtensions**: An array of file extensions to synchronize.
  - **Locations**: A list of locations to synchronize files to and from.
    - **Path**: The path to the location.
    - **Type**: The type of location (`Local`, `FileShare`, `Ftp`).
    - **Username**: (Optional) The username for accessing the location.
    - **Password**: (Optional) The password for accessing the location.
    - **FtpHost**: (Optional) The FTP host address.
    - **FtpPort**: (Optional) The FTP port.
    - **FtpHostTimezone**: (Optional) The timezone of the FTP host.

## Usage

1. Configure the `appsettings.json` file with your desired synchronization settings.
2. Run the LiveSync tool to start synchronizing your files.

## Running

To build LiveSync, use the following command:

```sh
dotnet build
```

This will compile the application and generate the necessary binaries.

To run LiveSync, use the following command:

```sh
dotnet run
```

This will start the application and begin synchronizing files based on the configuration provided in the `appsettings.json` file.

## Installing as a Windows Service

To install LiveSync as a Windows service, follow these steps:

1. Open a command prompt as an administrator.
2. Navigate to the directory where the LiveSync executable is located.
3. Run the following command to install the service:

    ```sh
    sc create LiveSync binPath= "C:\path\to\LiveSync.exe"
    ```

4. Start the service with the following command:

    ```sh
    sc start LiveSync
    ```

5. To stop the service, use the following command:

    ```sh
    sc stop LiveSync
    ```

6. To delete the service, use the following command:

    ```sh
    sc delete LiveSync
    ```

## License

This project is licensed under the MIT License.
