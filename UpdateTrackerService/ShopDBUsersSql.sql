DROP LOGIN UpdateTrackerService
GO

CREATE LOGIN UpdateTrackerService WITH PASSWORD = '123321'
GO

USE ShopDB
GO
CREATE USER UpdateTrackerService FOR LOGIN UpdateTrackerService
  WITH DEFAULT_SCHEMA = dbo

USE ShopDB
GO
EXEC sp_addrolemember 'db_owner', UpdateTrackerService
GO
