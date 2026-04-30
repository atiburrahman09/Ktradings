KTrading - User Guide

Overview

- KTrading is a small Razor Pages inventory and sales management system.
- Key modules: Products, Customers, Sales Orders, Returns, Inventory, Reports.

Getting started

1. Login
   - Open the app and login with the seeded admin user:
     - Email: admin@ktrading.local
     - Password: Admin123!
   - If you are redirected to the login page, enter credentials and click Sign in.

2. Navigation
   - After login you will see the Dashboard (cards) and the main menu with Products, Customers, Sales Orders, Product Returns, Inventory, Reports.

Users & Roles

- Seeded roles: Admin, Sales, Warehouse, Manager.
- Seeded admin user belongs to Admin role.
- Create pages require Admin or Sales role.

Products

- Navigate to Products -> Create to add products.
- Fields: SKU, Name, Unit, Price, Cost, VATPercent, IsActive.

Customers

- Add customers under Customers -> Create.
- Fields include Name, Code, ContactName, Phone, Email.

Sales Orders

- Create a new sales order via Sales Orders -> Create.
- Add items dynamically, select product, set quantity and unit price. Line totals and subtotal are calculated client-side.
- On create, stock is decremented and StockMovements are recorded.

Returns

- Create returns under Product Returns -> Create.
- Add returned items and mark damaged items.
- To process a return, view the return details and click Process Return. Processed returns will create StockMovements and restock undamaged items.

Inventory

- Inventory shows current stocks and links to stock details. StockMovements record IN/OUT/RETURN/DAMAGE.

Reports

- Reports -> Inventory Value shows inventory value based on product cost.
- Click Export as Excel to download a Summary sheet.
