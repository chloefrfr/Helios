# Setup Guide

This will be a guide on how to setup the project.

## **NOTE**

Not all fortnite versions are currently supported.

## **Prerequisites**

1. **[Install Dotnet 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0):**

   - Download and install the latest version of the .NET 8.0 SDK.
   - Verify the installation:

   ```bash
   dotnet --version
   ```

2. **[Visual Studio 2022](https://visualstudio.microsoft.com/vs/):**

   - Install the following packages:
     - `.NET desktop development`
     - `ASP.NET and web development`

3. **[PostgreSQL](https://www.postgresql.org/download/):**

   - Download and install the latest version of PostgreSQL.
   - During the installation, remember your username and password for the database.

4. **[Git](https://git-scm.com/downloads):**

   - Download and install Git.
   - Verify the installation:

   ```bash
   git --version
   ```

## **Setup**

# **Step 0: Cloning the Repository**

1. Open a terminal or command prompt.
2. Navigate to the directory where you want to clone the repository.
3. Clone the repository using the following command:

```bash
git clone --recursive https://github.com/chloefrfr/Helios.git
```

# **Step 1: Creating the `config.yml` File**

1. Navigate to the `Helios` folder then navigate to the `Assets` folder.
2. Create a new file named `config.yml` in the `Assets` folder.

3. Open the `config.yml` file in a ide or text editor.
4. Add the following data:

```yml
<?xml version="1.0" encoding="utf-8"?>
<config xmlns="http://fortmp.dev/config">
<DatabaseConnectionUrl>Host=localhost;Port=5432;Database=helios;Username=postgres;Password=3573</DatabaseConnectionUrl>
<JWTClientSecret>test123</JWTClientSecret>
<GameDirectory>F:\\skies\builds\\14.40\\FortniteGame\\Content\\Paks</GameDirectory>
<CurrentVersion>14.40</CurrentVersion>
</config>
```

3. Replace the values with your own.
   - **DatabaseConnectionUrl**: Your PostgreSQL database connection string.
   - **JWTClientSecret**: A secret string that will be used to sign your JWT tokens.
   - **GameDirectory**: Path to the Fortnite game directory (eg.. `D:\\FortniteBuilds\\Fortnite 12.41\\FortniteGame\\Content\\Paks`).
   - **CurrentVersion**: The build version (e.g.., `12.41`).

## **Step 2: Creating the Database**

1. Search `psql` in the start menu.
2. Login to your PostgreSQL server.
3. Create a new database using the following command:

```sql
CREATE DATABASE helios;
```

4. Exit

```sql
\q
```

## **Step 3: Running the Project**

1. Open the project in **Visual Studio 2022**.
2. Press `F5` to run the project.

## End

If you have any issues or questions, just create a new issue [here](https://github.com/chloefrfr/Helios/issues).
