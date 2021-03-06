﻿CREATE TABLE [DustyBot].[LfUser]
(
	[Id] int IDENTITY(1, 1) NOT NULL,
	[Username] nvarchar(100) UNIQUE NOT NULL,
	[Modified] datetime NOT NULL,
	CONSTRAINT [PK_LfUser_Id] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)