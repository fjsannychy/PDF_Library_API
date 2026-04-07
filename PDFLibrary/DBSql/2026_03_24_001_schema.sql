-- Users
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserName NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Status INT NOT NULL
);

-- Authors
CREATE TABLE Authors (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Address NVARCHAR(255) NULL,
    Contact NVARCHAR(100) NULL,
    Status INT NOT NULL
);

-- Publishers
CREATE TABLE Publishers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Address NVARCHAR(255) NULL,
    Contact NVARCHAR(100) NULL,
    Status INT NOT NULL
);

-- Categories
CREATE TABLE Categories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Status INT NOT NULL
);


-- Books
CREATE TABLE Books (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(255) NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    DiscountPercent DECIMAL(5,2) NULL,
    PriceBeforeDiscount DECIMAL(10,2) NULL,
    CategoryId INT NOT NULL,
    PublisherId INT NOT NULL,
    AuthorId INT NOT NULL,
    Edition NVARCHAR(50) NULL,
    Volume INT NULL,
    ShortDescription NVARCHAR(500) NULL,
    Details NVARCHAR(MAX) NULL,
    CoverPhotoUrl NVARCHAR(500) NULL,
    PdfUrl NVARCHAR(500) NULL,
    PublishDate DATETIME NULL,
    RegisterDate DATETIME NOT NULL DEFAULT GETDATE(),
    RegisteredBy INT NOT NULL,
    Status INT NOT NULL,

    CONSTRAINT FK_Books_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    CONSTRAINT FK_Books_Publisher FOREIGN KEY (PublisherId) REFERENCES Publishers(Id),
    CONSTRAINT FK_Books_Author FOREIGN KEY (AuthorId) REFERENCES Authors(Id),
    CONSTRAINT FK_Books_User FOREIGN KEY (RegisteredBy) REFERENCES Users(Id)
);

-- BookFeatures
CREATE TABLE BookFeatures (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(150) NOT NULL,
    BookId INT NOT NULL,
    Status INT NOT NULL,

    CONSTRAINT FK_BookFeatures_Book FOREIGN KEY (BookId) REFERENCES Books(Id)
);

-- BookAttachments
CREATE TABLE BookAttachments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    FileUrl NVARCHAR(500) NOT NULL,

    CONSTRAINT FK_BookAttachments_Book FOREIGN KEY (BookId) REFERENCES Books(Id)
);

-- BookReviews
CREATE TABLE BookReviews (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    UserId INT NOT NULL,
    Comment NVARCHAR(MAX) NULL,
    Rating DECIMAL(3,2) NOT NULL,

    CONSTRAINT FK_BookReviews_Book FOREIGN KEY (BookId) REFERENCES Books(Id),
    CONSTRAINT FK_BookReviews_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- UserFavourites
CREATE TABLE UserFavourites (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    UserId INT NOT NULL,

    CONSTRAINT FK_UserFavourites_Book FOREIGN KEY (BookId) REFERENCES Books(Id),
    CONSTRAINT FK_UserFavourites_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- UserActions
CREATE TABLE UserActions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    UserId INT NOT NULL,
    ActionType INT NOT NULL,
    ActionTime DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_UserActions_Book FOREIGN KEY (BookId) REFERENCES Books(Id),
    CONSTRAINT FK_UserActions_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- UserMarks
CREATE TABLE UserMarks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    UserId INT NOT NULL,
    PageNumber INT NOT NULL,
    PositionTopX FLOAT NOT NULL,
    PositionTopY FLOAT NOT NULL,
    PositionBottomX FLOAT NOT NULL,
    PositionBottomY FLOAT NOT NULL,
    MarkingTime DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_UserMarks_Book FOREIGN KEY (BookId) REFERENCES Books(Id),
    CONSTRAINT FK_UserMarks_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);


CREATE TABLE UserPayments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    UserId INT NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    PaymentAccount NVARCHAR(150) NULL,
    PaymentType INT NOT NULL,
    PaymentRef NVARCHAR(150) NULL,
    PaymentTime DATETIME NOT NULL DEFAULT GETDATE(),
    Remarks NVARCHAR(500) NULL,

    CONSTRAINT FK_UserPayments_Book FOREIGN KEY (BookId) REFERENCES Books(Id),
    CONSTRAINT FK_UserPayments_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);


CREATE TABLE RefreshTokens (
    Token NVARCHAR(500) PRIMARY KEY,
    UserId INT NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    IsRevoked BIT NOT NULL DEFAULT 0
);
