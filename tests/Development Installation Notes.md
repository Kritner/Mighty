# Development Installation Notes:

## Quick Install

All drivers for all databases (even SQL Server) for .NET Core are NuGet packages. The comments on drivers below are about the .NET Framework driver(s) for each database.

### SQL Server

 - SQL Server Development Edition
 - AdventureWorks database
 - Run Mighty test scripts:
   - SP Tests (install into AdventureWorks)
   - Write Tests DB (creates its own DB)
 - The `System.Data.SqlClient` ADO.NET driver is always available by default

### SQLite

 - Download and save the SQLite test database `Chinook_Sqlite_AutoIncrementPKs.sqlite` from https://github.com/lerocha/chinook-database/tree/master/ChinookDatabase/DataSources
 - No other installation is needed!
 - The `System.Data.SQLite` ADO.NET driver for .NET framework is always available by default (with Windows?), as far as I can make out
 - The `Microsoft.Data.Sqlite` driver for .NET Core is a NuGet download, as is standard for all databases with .NET Core

SQL Server:
-----------

- SQL Server Developer Edition (full feature, free, development only) should be fine for testing
- The test database is Adventure Works (Adventure Works 2014 and 2017 work fine; probably others too)
    o Download and the restore from `AdventureWorks2014.bak`, from:
      https://docs.microsoft.com/en-us/sql/samples/adventureworks-install-configure
    o Some additional SPs have been added (run `MightyTests\Sql\SqlServerSPTests.sql` to create)

- There is also a separate test database for writes (run `MightyTests\Sql\WriteTestDB_Create.sql` to create)

Oracle:
-------

- Unzip ODP.NET_Managed_ODAC12cR4.zip
    o This is for Oracle managed data access client (oracle.manageddataaccess.client)
    o Run as Administrator 'install_odpm.bat C:\Oracle both' (installs x86 and x64 - I may have only done x64 before, hence those tests failing?)

- Unzip OracleXE112_Win64.zip (Oracle Database 11g Express Edition)
    o Run setup.exe to install 
    o Choose a password for SYS and SYSTEM accounts (I chose the GRB db admin password)
    o For now, add oravirtualnerd as an alias to 127.0.0.1 to C:\Windows\System32\drivers\etc\hosts
    o You can already connect from a command prompt, use:
        `SQLPLUS / AS SYSDBA` (see https://docs.oracle.com/cd/B25329_01/doc/admin.102/b25107/startup.htm)
    o From here, you can install the SCOTT database (http://www.orafaq.com/wiki/SCOTT)
        `@ ?/rdbms/admin/scott.sql`
        `ALTER USER scott ACCOUNT UNLOCK;`
    o To check, you can now connect to this DB from the command line:
        `SQLPLUS SCOTT\TIGER`
        And `SELECT * FROM emp;` should work
    
- Unzip sqldeveloper-4.1.5.21.78-x64.zip (Oracle SQL Developer)
    o This is the program itself, it does not need further installation
    o Connect to localhost as SYSTEM/<password>

NB The Oracle tests WILL FAIL unless Test/Test Settings/Default Processor Architecture is set to X64

SQLite:
-------

- Tools for managing the SQLite DB are in sqlite-tools-win32-x86-3170000.zip, but these are not needed to run the tests

- The Microsoft SQLite data provider must be installed (`Microsoft.Data.Sqlite` for .NET Core, or `System.Data.SQLite` for .NET Framework)
  - I have never had to install these, they seem to be available by default from somwhere (with Windows? with Visual Studio?).

- The database for all SQLite tests is the autonumber variant of the Chinook example DB for SQLite,
  i.e. `Chinook_Sqlite_AutoIncrementPKs.sqlite` from https://github.com/lerocha/chinook-database/tree/master/ChinookDatabase/DataSources

PostgreSQL:
-----------

- Npgsql-3.2.0.msi
    o This is for PostgreSQL
    o "Installs Npgsql into the GAC and adds it to your machine.config. This is not the recommended way to use Npgsql (use nuget instead)."
    o For now, I have done this.


Install the database itself from: PostgreSQL-9.6.1-1-win64-bigsql.exe
You'll want to install pgAdmin as well
Note: BigSQL Manager lives on localhost:8050
!Make sure the PostgreSQL is higher up your path than Oracle, beware that createdb also exists as a command for both
Create the Northwind database by unzipping northwind_psql-master.zip and then running create_db.sh from within it - to make it run as is you'd need to add -U postgres as an option to each line and convert it to a .bat or .cmd extension
By default the postgres user doesn't require a password, you should use psql to set one.
Bizarrely, the postgres user works with ANY password. I assume this is only when connecting from localhost. TO DO: test.

MySQL:
------

- Install the latest "dotConnect for MySQL (Express Edition)" (i.e. the free version of `Devart.Data.MySql` data provider)
  using `dcmysqlfree.exe` (you don't need the help files or the Visual Studio integration; you do need to install the provider
  to the GAC) [NB you need to have installed (this way) the version which your project loads from NuGet, or the license will not work]

- Install the server from https://dev.mysql.com/downloads/mysql/
    o Note the comment "Note: MySQL Installer is 32 bit, but will install both 32 bit and 64 bit binaries."
    o Download the web installer version to only pull the parts you actually need from the web during the install
    o NB If you have previously installed MySQL you will need to uninstall everything in order to select a different Setup Type (e.g. to change from 'Client Only' to 'Full' or 'Custom')

- You require MySQL Server on the server and Connector/NET on the client, and you will probablty want MySQL Workbench

- I chose old (weaker) style authenticaion at install, and I need to see what happens if I choose the newer
  stronger (SHA-256) version, i.e. which drivers, if any, support it

- Download and install the Sakila test database for MySQL (https://dev.mysql.com/doc/sakila/en/sakila-installation.html)
    o shell> mysql -u root -p
    o mysql> SOURCE C:/temp/sakila-db/sakila-schema.sql;
    o mysql> SOURCE C:/temp/sakila-db/sakila-data.sql;
    o mysql> CREATE USER 'Massive'@'localhost' IDENTIFIED BY 'mt123';
    o mysql> GRANT ALL ON sakila.* TO 'Massive'@'localhost';
    o If it doesn't appear in MySQL Workbench, try clicking the refresh icon for the Schemas view

- To use with older drivers, you may need to do steps from:
    o https://dev.mysql.com/doc/refman/8.0/en/upgrading-from-previous-series.html#upgrade-caching-sha2-password

- Create database (from Massive script) and then user permissions (as just above) for MassiveWriteTests db

- Run MySQP_SPTests.sql in database Sakila

- NB The DevartTests and SPTests in MySql.Data.MySqlClient require higher privileges than above (at least BackupAdmin administrative
  role, if set through MySQL Workbench), I am not sure why. Without this:
    o Devart_ParameterCheck fails with "Can not the describe procedure Sakila.testproc_in_out. You do not have enough privileges to get object metadata." [sic]
      (TO DO: Wording is minor bug in Devart.)
    o SPTests for ("MySql.Data.MySqlClient") fail with "System.Data.SqlTypes.SqlNullValueException : Data is Null. This method or property cannot be called on Null values."
      (TO DO: Looks like a bug in the MySqlClient, since the Devart client is passing the same tests with less permissions.)

- TO DO:
    o On .NET Core only, I am seeing "MySql.Data.MySqlClient.MySqlException : Table 'mysql.proc' doesn't exist" on calls involving stored procs
    o At the moment, this strongly appears to be a problem with 'MySql.Data.MySqlClient' driver on .NET Core:
        - This table *doesn't * exist anymore in MySQL 8.0 and isn't supposed to
          (see https://ocelot.ca/blog/blog/2017/08/22/no-more-mysql-proc-in-mysql-8-0/ )
          and its not existing isn't a problem for running stored procedures in the .NET Framework code
    o Resolution - upgrade the MySql.Data NuGet package from 7.0.6-IR31 to 8.0.15 so that it works properly with MySQL v8 databases!

Firebird:
---------

TO DO :)