-- Gravity Maze MySQL mirror schema. Run once in MySQL Workbench (File > Open SQL Script, then the lightning bolt).
-- records.xml stays the main save; the game copies it into these tables after every save.
-- No foreign keys on purpose: the mirror must never reject something the XML accepted.

CREATE DATABASE IF NOT EXISTS gravitymaze CHARACTER SET utf8mb4;
USE gravitymaze;

CREATE TABLE IF NOT EXISTS players (
    playerKey     VARCHAR(100) NOT NULL,
    name          VARCHAR(100) NOT NULL,
    created       DATETIME     NOT NULL,
    lastPlayed    DATETIME     NOT NULL,
    unlockedLevel INT          NOT NULL,
    ghost         BOOLEAN      NOT NULL,
    PRIMARY KEY (playerKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS runs (
    id             INT           NOT NULL,   -- 1-based position in the journal
    player         VARCHAR(100)  NOT NULL,
    playerKey      VARCHAR(100)  NOT NULL,
    level          INT           NOT NULL,   -- 0 = full campaign
    campaignLevels INT           NULL,       -- only for level 0
    outcome        VARCHAR(20)   NOT NULL,
    time           DECIMAL(10,2) NOT NULL,
    attempts       INT           NOT NULL,
    stars          INT           NOT NULL,
    score          INT           NOT NULL,
    wallHits       INT           NOT NULL,
    falls          INT           NOT NULL,
    heartsLost     FLOAT         NOT NULL,
    barriersBroken INT           NOT NULL,
    date           DATETIME      NOT NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ghosts (
    playerKey VARCHAR(100)  NOT NULL,
    level     INT           NOT NULL,
    time      DECIMAL(10,2) NOT NULL,
    stepMs    FLOAT         NOT NULL,
    points    MEDIUMTEXT    NOT NULL,        -- x,y;x,y
    PRIMARY KEY (playerKey, level)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS settings (
    id              TINYINT      NOT NULL,   -- always 1
    LastPlayer      VARCHAR(100) NOT NULL,
    FullTiltDegrees FLOAT        NOT NULL,
    Deadzone        FLOAT        NOT NULL,
    Smoothing       FLOAT        NOT NULL,
    SwapAxes        BOOLEAN      NOT NULL,
    InvertX         BOOLEAN      NOT NULL,
    InvertY         BOOLEAN      NOT NULL,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Optional: a dedicated login instead of root. Uncomment, pick a password, then put it in mysql.ini.
-- CREATE USER 'gravitymaze'@'localhost' IDENTIFIED BY 'choose-a-password';
-- GRANT ALL ON gravitymaze.* TO 'gravitymaze'@'localhost';
