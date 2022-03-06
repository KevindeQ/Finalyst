-- Table: test
CREATE TABLE test
(
   id INT NOT NULL,
   username NVARCHAR(500) NOT NULL,
   comment NVARCHAR(500) NULL
);

GO

INSERT INTO test (id, username, comment)
VALUES
    (0, 'Name 1', 'Some random text 1'),
    (1, 'Name 2', 'Some random text 2'),
    (2, 'Name 3', 'Some random text 3'),
    (3, 'Name 4', 'Some random text 4')

GO