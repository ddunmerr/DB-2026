USE pc_shop;
GO

-- ============================================
-- 1. Категории
-- ============================================
SET IDENTITY_INSERT category ON;

INSERT INTO category (id, title, slug, parent_id) VALUES
(1, N'Комплектующие', 'components', NULL),
(2, N'Процессоры',    'cpu',         1),
(3, N'Материнские платы', 'motherboard', 1),
(4, N'Оперативная память', 'ram',     1),
(5, N'Видеокарты',    'gpu',         1),
(6, N'Блоки питания', 'psu',         1),
(7, N'Корпуса',       'case',        1);

SET IDENTITY_INSERT category OFF;
GO

-- ============================================
-- 2. Товары
-- ============================================
SET IDENTITY_INSERT product ON;

INSERT INTO product (id, title, brand, image_url, price, stock, specs, category_id) VALUES
-- Процессоры
(1, N'AMD Ryzen 7 7800X3D', N'AMD', N'/img/products/ryzen-7800x3d.jpg', 44990.00, 15,
 N'{"socket":"AM5","tdp":120,"cores":8,"threads":16,"ram_type":"DDR5","max_ram_speed":5200,"has_igpu":true}', 2),

(2, N'Intel Core i5-14600K', N'Intel', N'/img/products/core-i5-14600K.jpg', 32990.00, 20,
 N'{"socket":"LGA1700","tdp":125,"cores":14,"threads":20,"ram_type":"DDR5","max_ram_speed":5600,"has_igpu":true}', 2),

-- Материнские платы
(3, N'ASUS TUF Gaming B650-PLUS', N'ASUS', N'/img/products/asus-tuf-gaming-b650-plus.jpg', 18990.00, 10,
 N'{"socket":"AM5","chipset":"B650","form_factor":"ATX","ram_type":"DDR5","ram_slots":4,"max_ram_speed":6400,"m2_slots":2,"sata_ports":4}', 3),

(4, N'MSI PRO Z790-A WIFI', N'MSI', N'/img/products/msi-pro-z790-a-wifi.jpg', 24990.00, 8,
 N'{"socket":"LGA1700","chipset":"Z790","form_factor":"ATX","ram_type":"DDR5","ram_slots":4,"max_ram_speed":7200,"m2_slots":3,"sata_ports":6}', 3),

-- Оперативная память
(5, N'Kingston Fury Beast DDR5 32GB', N'Kingston', N'/img/products/kingstonfury-beast-ddr5-32gb.jpg', 12490.00, 25,
 N'{"ram_type":"DDR5","capacity_gb":32,"modules":2,"speed":6000}', 4),

(6, N'G.Skill Trident Z5 DDR5 64GB', N'G.Skill', N'/img/products/g-skill-trident-z5-ddr5-64gb.jpg', 24990.00, 12,
 N'{"ram_type":"DDR5","capacity_gb":64,"modules":2,"speed":6400}', 4),

-- Видеокарты
(7, N'NVIDIA GeForce RTX 4070 Super', N'NVIDIA', N'/img/products/rtx-4070-super.jpg', 74990.00, 7,
 N'{"length_mm":305,"tdp":220,"power_connectors":["8-pin","8-pin"]}', 5),

(8, N'AMD Radeon RX 7800 XT', N'AMD', N'/img/products/rx-7800-xt.jpg', 64990.00, 9,
 N'{"length_mm":280,"tdp":263,"power_connectors":["8-pin","8-pin"]}', 5),

-- Блоки питания
(9, N'Corsair RM750e 750W', N'Corsair', N'/img/products/corsair-rm750e-750w.jpg', 11990.00, 18,
 N'{"wattage":750,"efficiency":"80+ Gold","form_factor":"ATX"}', 6),

(10, N'Seasonic Focus GX-850 850W', N'Seasonic', N'/img/products/seasonic-focus-gx-850.jpg', 15990.00, 14,
 N'{"wattage":850,"efficiency":"80+ Gold","form_factor":"ATX"}', 6),

-- Корпуса
(11, N'Fractal Design Meshify 2', N'Fractal Design', N'fractal-design-meshify-2.jpg', 14990.00, 11,
 N'{"supported_form_factors":["ATX","Micro-ATX","Mini-ITX"],"max_gpu_length":360,"max_cooler_height":170}', 7),

(12, N'Cooler Master NR200P', N'Cooler Master', N'/img/products/cooler-master-nr200p.jpg', 9990.00, 6,
 N'{"supported_form_factors":["Mini-ITX"],"max_gpu_length":330,"max_cooler_height":155}', 7);

SET IDENTITY_INSERT product OFF;
GO

-- ============================================
-- 3. Пользователи
-- ============================================
SET IDENTITY_INSERT customer ON;

INSERT INTO customer (id, email, username, pass) VALUES
(1, N'alice@example.com', N'Alice', N'123'),
(2, N'bob@example.com',   N'Bob',   N'321');

SET IDENTITY_INSERT customer OFF;
GO

-- ============================================
-- 4. Корзины (по одной на пользователя)
-- ============================================
SET IDENTITY_INSERT cart ON;

INSERT INTO cart (id, customer_id) VALUES
(1, 1),
(2, 2);

SET IDENTITY_INSERT cart OFF;
GO

-- ============================================
-- 5. Позиции корзин
-- ============================================
INSERT INTO cart_item (cart_id, product_id, quantity) VALUES
(1, 1, 1),  -- Alice: Ryzen 7 7800X3D
(1, 3, 1),  -- Alice: ASUS B650
(1, 5, 2),  -- Alice: Kingston 32GB x2
(2, 7, 1);  -- Bob: RTX 4070 Super
GO

-- ============================================
-- 6. Заказы
-- ============================================
SET IDENTITY_INSERT [order] ON;

INSERT INTO [order] (id, customer_id, status) VALUES
(1, 1, 'paid'),
(2, 2, 'pending');

SET IDENTITY_INSERT [order] OFF;
GO

-- ============================================
-- 7. Позиции заказов
-- ============================================
INSERT INTO order_item (order_id, product_id, quantity, price_at_purchase) VALUES
(1, 1, 1, 44990.00),
(1, 3, 1, 18990.00),
(2, 7, 1, 74990.00);
GO

-- ============================================
-- 8. Отзывы
-- ============================================
INSERT INTO review (customer_id, product_id, rating, content) VALUES
(1, 1, 5, N'Отличный процессор для игр. X3D-кэш решает.'),
(2, 7, 4, N'Мощная карта, но греется под нагрузкой.');
GO

-- ============================================
-- 9. История цен
-- ============================================
INSERT INTO price_history (product_id, price) VALUES
(1, 49990.00),
(1, 46990.00),
(1, 44990.00),
(7, 79990.00),
(7, 74990.00);
GO