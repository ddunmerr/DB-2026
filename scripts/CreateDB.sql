CREATE DATABASE pc_shop;
GO

USE pc_shop;
GO

CREATE TABLE category (
    id        INT IDENTITY(1,1) PRIMARY KEY,
    title      NVARCHAR(100) NOT NULL,
    slug      VARCHAR(50)   NOT NULL UNIQUE,
    parent_id INT           NULL,

    CONSTRAINT fk_category_parent
        FOREIGN KEY (parent_id) REFERENCES category(id)
);

CREATE TABLE product (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    title        NVARCHAR(200) NOT NULL,
    brand       NVARCHAR(100) NULL,
    image_url   NVARCHAR(500) NULL,
    price       DECIMAL(10,2) NOT NULL,
    stock       INT           NOT NULL DEFAULT 0,
    specs       NVARCHAR(MAX) NOT NULL,
    category_id INT           NOT NULL,
    created_at  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT fk_product_category
        FOREIGN KEY (category_id) REFERENCES category(id),

    CONSTRAINT chk_product_specs CHECK (ISJSON(specs) = 1)
);

CREATE TABLE customer (
    id            INT IDENTITY(1,1) PRIMARY KEY,
    email         NVARCHAR(255) NOT NULL UNIQUE,
    username          NVARCHAR(100) NULL,
    pass NVARCHAR(255) NOT NULL,
    created_at    DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE cart (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT           NOT NULL,
    created_at  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT fk_cart_customer
        FOREIGN KEY (customer_id) REFERENCES customer(id)
        ON DELETE CASCADE,

    CONSTRAINT uq_cart_customer UNIQUE (customer_id)
);

CREATE TABLE cart_item (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    cart_id    INT NOT NULL,
    product_id INT NOT NULL,
    quantity   INT NOT NULL DEFAULT 1,

    CONSTRAINT fk_cart_item_cart
        FOREIGN KEY (cart_id) REFERENCES cart(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_cart_item_product
        FOREIGN KEY (product_id) REFERENCES product(id),

    CONSTRAINT uq_cart_item UNIQUE (cart_id, product_id)
);

CREATE TABLE [order] (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT           NOT NULL,
    status      VARCHAR(20)   NOT NULL DEFAULT 'pending',
    created_at  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT fk_order_customer
        FOREIGN KEY (customer_id) REFERENCES customer(id)
);

CREATE TABLE order_item (
    id                INT IDENTITY(1,1) PRIMARY KEY,
    order_id          INT           NOT NULL,
    product_id        INT           NOT NULL,
    quantity          INT           NOT NULL,
    price_at_purchase DECIMAL(10,2) NOT NULL,

    CONSTRAINT fk_order_item_order
        FOREIGN KEY (order_id) REFERENCES [order](id)
        ON DELETE CASCADE,

    CONSTRAINT fk_order_item_product
        FOREIGN KEY (product_id) REFERENCES product(id)
);

CREATE TABLE review (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    customer_id INT           NOT NULL,
    product_id  INT           NOT NULL,
    rating      INT           NOT NULL,
    content        NVARCHAR(MAX) NULL,
    created_at  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT fk_review_customer
        FOREIGN KEY (customer_id) REFERENCES customer(id),

    CONSTRAINT fk_review_product
        FOREIGN KEY (product_id) REFERENCES product(id)
        ON DELETE CASCADE,

    CONSTRAINT uq_review_customer_product UNIQUE (customer_id, product_id)
);

CREATE TABLE price_history (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    product_id INT           NOT NULL,
    price      DECIMAL(10,2) NOT NULL,
    changed_at DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT fk_price_history_product
        FOREIGN KEY (product_id) REFERENCES product(id)
        ON DELETE CASCADE
);