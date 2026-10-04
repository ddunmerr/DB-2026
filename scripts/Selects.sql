-- Все товары с категориями
SELECT p.id, p.title, p.brand, p.image_url, p.price, c.title AS category
FROM product p
JOIN category c ON c.id = p.category_id;

-- Все CPU
SELECT p.title, JSON_VALUE(p.specs, '$.socket') AS socket,
       JSON_VALUE(p.specs, '$.tdp') AS tdp
FROM product p
JOIN category c ON c.id = p.category_id
WHERE c.slug = 'cpu';

-- Корзина
SELECT p.title, ci.quantity
FROM cart_item ci
JOIN cart c ON c.id = ci.cart_id
JOIN product p ON p.id = ci.product_id
WHERE c.customer_id = 1;
