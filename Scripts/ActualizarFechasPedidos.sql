-- Script para actualizar fechas de pedidos que tienen valor por defecto
-- Ejecutar este script en la base de datos GestionComprasP del servidor

USE GestionComprasP;

-- Actualizar pedidos con fecha por defecto (0001-01-01) a la fecha actual
-- o a una fecha razonable basada en el Id del pedido
UPDATE Pedido 
SET FechaPedido = CURDATE()
WHERE FechaPedido = '0001-01-01' OR FechaPedido IS NULL OR FechaPedido = '0000-00-00';

-- Verificar los cambios
SELECT Id, NumeroPedido, ItemCodigo, FechaPedido, Estado 
FROM Pedido 
ORDER BY Id DESC 
LIMIT 20;
