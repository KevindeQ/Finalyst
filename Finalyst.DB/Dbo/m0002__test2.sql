-- Table: test
CREATE TABLE test2
(
   id INT NOT NULL,
   stock_name NVARCHAR(500) NOT NULL,
   reference_date DATETIME NOT NULL
);

GO

INSERT INTO test (id, username, comment)
VALUES
    (0, 'AAPL', GETDATE()),
    (1, 'NFLX', GETDATE()),
    (2, 'TSLA', GETDATE()),
    (3, 'MSFT', GETDATE())

GO