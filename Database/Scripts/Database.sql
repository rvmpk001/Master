-- ============================================================================
-- SCRIPT DE CREACION Y CARGA DE DATOS: DemoShowcaseDB
-- Solucion: WINFORMS CONTROL SHOWCASE
-- Visual Basic .NET / Windows Forms / SQL Server Express / ADO.NET
-- ============================================================================

USE [master];
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DemoShowcaseDB')
BEGIN
    CREATE DATABASE [DemoShowcaseDB];
    PRINT 'Base de datos DemoShowcaseDB creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La base de datos DemoShowcaseDB ya existe.';
END
GO

USE [DemoShowcaseDB];
GO

-- ============================================================================
-- 1. TABLA: Configuracion
-- ============================================================================
IF OBJECT_ID(N'dbo.Configuracion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Configuracion (
        Clave NVARCHAR(50) NOT NULL,
        Valor NVARCHAR(500) NOT NULL,
        Descripcion NVARCHAR(250) NULL,
        UltimaModificacion DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Configuracion PRIMARY KEY (Clave)
    );
    PRINT 'Tabla Configuracion creada.';
END
GO

-- ============================================================================
-- 2. TABLA: Usuarios
-- ============================================================================
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        IdUsuario INT IDENTITY(1,1) NOT NULL,
        Username NVARCHAR(50) NOT NULL,
        PasswordHash NVARCHAR(128) NOT NULL,
        NombreCompleto NVARCHAR(150) NOT NULL,
        Rol NVARCHAR(30) NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaRegistro DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Usuarios PRIMARY KEY (IdUsuario),
        CONSTRAINT UQ_Usuarios_Username UNIQUE (Username)
    );
    CREATE NONCLUSTERED INDEX IX_Usuarios_Rol ON dbo.Usuarios (Rol);
    PRINT 'Tabla Usuarios creada.';
END
GO

-- ============================================================================
-- 3. TABLA: Categorias
-- ============================================================================
IF OBJECT_ID(N'dbo.Categorias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categorias (
        IdCategoria INT IDENTITY(1,1) NOT NULL,
        Nombre NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(255) NULL,
        Activo BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_Categorias PRIMARY KEY (IdCategoria),
        CONSTRAINT UQ_Categorias_Nombre UNIQUE (Nombre)
    );
    PRINT 'Tabla Categorias creada.';
END
GO

-- ============================================================================
-- 4. TABLA: Productos
-- ============================================================================
IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Productos (
        IdProducto INT IDENTITY(1,1) NOT NULL,
        IdCategoria INT NOT NULL,
        Codigo NVARCHAR(50) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        PrecioUnitario DECIMAL(18,2) NOT NULL,
        Stock INT NOT NULL DEFAULT 0,
        ImagenBase64 NVARCHAR(MAX) NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaCreacion DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Productos PRIMARY KEY (IdProducto),
        CONSTRAINT UQ_Productos_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_Productos_Categorias FOREIGN KEY (IdCategoria) REFERENCES dbo.Categorias (IdCategoria),
        CONSTRAINT CK_Productos_Precio CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_Productos_Stock CHECK (Stock >= 0)
    );
    CREATE NONCLUSTERED INDEX IX_Productos_IdCategoria ON dbo.Productos (IdCategoria);
    CREATE NONCLUSTERED INDEX IX_Productos_Nombre ON dbo.Productos (Nombre);
    PRINT 'Tabla Productos creada.';
END
GO

-- ============================================================================
-- 5. TABLA: Clientes
-- ============================================================================
IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes (
        IdCliente INT IDENTITY(1,1) NOT NULL,
        Documento NVARCHAR(20) NOT NULL,
        Nombres NVARCHAR(100) NOT NULL,
        Apellidos NVARCHAR(100) NOT NULL,
        Telefono NVARCHAR(30) NULL,
        Email NVARCHAR(100) NULL,
        Direccion NVARCHAR(200) NULL,
        Ciudad NVARCHAR(80) NOT NULL DEFAULT 'Santo Domingo',
        Preferencias NVARCHAR(250) NULL,
        LimiteCredito DECIMAL(18,2) NOT NULL DEFAULT 5000.00,
        FechaNacimiento DATE NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaRegistro DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Clientes PRIMARY KEY (IdCliente),
        CONSTRAINT UQ_Clientes_Documento UNIQUE (Documento)
    );
    CREATE NONCLUSTERED INDEX IX_Clientes_Ciudad ON dbo.Clientes (Ciudad);
    CREATE NONCLUSTERED INDEX IX_Clientes_ApellidosNombres ON dbo.Clientes (Apellidos, Nombres);
    PRINT 'Tabla Clientes creada.';
END
GO

-- ============================================================================
-- 6. TABLA: Ventas
-- ============================================================================
IF OBJECT_ID(N'dbo.Ventas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ventas (
        IdVenta INT IDENTITY(1,1) NOT NULL,
        NumeroFactura NVARCHAR(30) NOT NULL,
        IdCliente INT NOT NULL,
        IdUsuario INT NOT NULL,
        FechaVenta DATETIME NOT NULL DEFAULT GETDATE(),
        Subtotal DECIMAL(18,2) NOT NULL,
        Impuesto DECIMAL(18,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        MetodoPago NVARCHAR(50) NOT NULL DEFAULT 'Efectivo',
        Observaciones NVARCHAR(300) NULL,
        Estado NVARCHAR(20) NOT NULL DEFAULT 'Completada',
        CONSTRAINT PK_Ventas PRIMARY KEY (IdVenta),
        CONSTRAINT UQ_Ventas_NumeroFactura UNIQUE (NumeroFactura),
        CONSTRAINT FK_Ventas_Clientes FOREIGN KEY (IdCliente) REFERENCES dbo.Clientes (IdCliente),
        CONSTRAINT FK_Ventas_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario)
    );
    CREATE NONCLUSTERED INDEX IX_Ventas_FechaVenta ON dbo.Ventas (FechaVenta);
    CREATE NONCLUSTERED INDEX IX_Ventas_IdCliente ON dbo.Ventas (IdCliente);
    PRINT 'Tabla Ventas creada.';
END
GO

-- ============================================================================
-- 7. TABLA: DetalleVenta
-- ============================================================================
IF OBJECT_ID(N'dbo.DetalleVenta', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetalleVenta (
        IdDetalle INT IDENTITY(1,1) NOT NULL,
        IdVenta INT NOT NULL,
        IdProducto INT NOT NULL,
        Cantidad INT NOT NULL,
        PrecioUnitario DECIMAL(18,2) NOT NULL,
        Subtotal DECIMAL(18,2) NOT NULL,
        CONSTRAINT PK_DetalleVenta PRIMARY KEY (IdDetalle),
        CONSTRAINT FK_DetalleVenta_Ventas FOREIGN KEY (IdVenta) REFERENCES dbo.Ventas (IdVenta) ON DELETE CASCADE,
        CONSTRAINT FK_DetalleVenta_Productos FOREIGN KEY (IdProducto) REFERENCES dbo.Productos (IdProducto),
        CONSTRAINT CK_DetalleVenta_Cantidad CHECK (Cantidad > 0)
    );
    CREATE NONCLUSTERED INDEX IX_DetalleVenta_IdVenta ON dbo.DetalleVenta (IdVenta);
    CREATE NONCLUSTERED INDEX IX_DetalleVenta_IdProducto ON dbo.DetalleVenta (IdProducto);
    PRINT 'Tabla DetalleVenta creada.';
END
GO

-- ============================================================================
-- INSERCIÓN DE DATOS DE EJEMPLO
-- ============================================================================

-- Configuracion
IF NOT EXISTS (SELECT 1 FROM dbo.Configuracion WHERE Clave = 'Empresa_Nombre')
BEGIN
    INSERT INTO dbo.Configuracion (Clave, Valor, Descripcion) VALUES
    ('Empresa_Nombre', 'Showcase Corporation S.A.', 'Nombre comercial de la empresa'),
    ('Empresa_RNC', '1-01-99887-2', 'Identificación tributaria fiscal'),
    ('Empresa_Telefono', '+1 (809) 555-0199', 'Teléfono principal de contacto'),
    ('Empresa_Email', 'contacto@showcasecorp.com', 'Correo central de soporte y ventas'),
    ('Empresa_Direccion', 'Av. Winston Churchill #105, Torre Empresarial Piso 8', 'Ubicación de la sede principal'),
    ('Sistema_Moneda', 'USD', 'Moneda principal del sistema'),
    ('Sistema_ImpuestoPorcentaje', '18.00', 'Porcentaje de impuesto sobre ventas (ITBIS/IVA)'),
    ('Sistema_TemaVisual', 'Moderno Oscuro', 'Tema visual predeterminado del aplicativo'),
    ('Sistema_ColorPrimario', '#0078D7', 'Color primario de acento'),
    ('Sistema_ColorSecundario', '#107C41', 'Color secundario de acento'),
    ('Sistema_TamanoFuente', '9.5', 'Tamaño de fuente base en puntos');
    PRINT 'Configuraciones iniciales insertadas.';
END
GO

-- 5 Usuarios
-- Password por defecto para todos: 'admin123' o 'demo123'
-- Hash SHA256 de 'admin123' = '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9'
-- Hash SHA256 de '123456'   = '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92'
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Usuarios (Username, PasswordHash, NombreCompleto, Rol, Email, Activo) VALUES
    ('admin', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 'Rafael Administrador Senior', 'Administrador', 'admin@showcasecorp.com', 1),
    ('supervisor', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 'María Fernandez Peralta', 'Supervisor', 'mfernandez@showcasecorp.com', 1),
    ('cajero1', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 'Carlos Alberto Gómez', 'Cajero', 'cgomez@showcasecorp.com', 1),
    ('cajero2', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 'Laura Elena Santana', 'Cajero', 'lsantana@showcasecorp.com', 1),
    ('vendedor1', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 'Jorge Luis Morales', 'Vendedor', 'jmorales@showcasecorp.com', 1);
    PRINT '5 Usuarios insertados.';
END
GO

-- 10 Categorias
IF NOT EXISTS (SELECT 1 FROM dbo.Categorias WHERE Nombre = 'Computadoras y Laptops')
BEGIN
    INSERT INTO dbo.Categorias (Nombre, Descripcion, Activo) VALUES
    ('Computadoras y Laptops', 'Equipos portátiles, laptops gamer y PCs de escritorio', 1),
    ('Monitores y Pantallas', 'Monitores IPS, 4K, pantallas curvas y paneles profesionales', 1),
    ('Componentes de Hardware', 'Procesadores, tarjetas de video, placas madre y RAM', 1),
    ('Almacenamiento Digital', 'Discos de estado sólido SSD, NVMe y discos duros mecánicos', 1),
    ('Periféricos y Entrada', 'Teclados mecánicos, ratones gamer y pads de precisión', 1),
    ('Audio y Multimedia', 'Auriculares de alta fidelidad, micrófonos y altavoces', 1),
    ('Redes y Conectividad', 'Routers WiFi 6, switches gigabit y adaptadores de red', 1),
    ('Impresión y Escaneo', 'Impresoras multifuncionales láser y consumibles de tinta', 1),
    ('Accesorios y Cables', 'Cables HDMI 2.1, DisplayPort, cargadores y hubs USB-C', 1),
    ('Software y Licencias', 'Sistemas operativos, suites ofimáticas y herramientas dev', 1);
    PRINT '10 Categorías insertadas.';
END
GO

-- 20 Clientes
IF NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE Documento = '001-1234567-8')
BEGIN
    INSERT INTO dbo.Clientes (Documento, Nombres, Apellidos, Telefono, Email, Direccion, Ciudad, Preferencias, LimiteCredito, FechaNacimiento, Activo) VALUES
    ('001-1234567-8', 'Juan Pablo', 'Duarte Reyes', '809-555-1001', 'jp.duarte@email.com', 'Calle El Conde #45', 'Santo Domingo', 'Tecnología, Audio', 15000.00, '1985-01-26', 1),
    ('001-2345678-9', 'Ana Rosa', 'Castillo Mendoza', '809-555-1002', 'ana.castillo@email.com', 'Av. 27 de Febrero #230', 'Santo Domingo', 'Laptops, Monitores', 25000.00, '1990-05-14', 1),
    ('001-3456789-0', 'Miguel Ángel', 'Torres Peña', '809-555-1003', 'm.torres@email.com', 'Calle del Sol #12', 'Santiago', 'Componentes, Gaming', 30000.00, '1988-11-03', 1),
    ('001-4567890-1', 'Patricia', 'Guerrero Silva', '809-555-1004', 'pguerrero@email.com', 'Av. Bartolomé Colón #88', 'Santiago', 'Oficina, Accesorios', 10000.00, '1992-03-22', 1),
    ('001-5678901-2', 'Roberto', 'Navarro Ortiz', '809-555-1005', 'r.navarro@email.com', 'Av. Juan Pablo Duarte #15', 'La Vega', 'Audio, Redes', 12000.00, '1983-09-19', 1),
    ('001-6789012-3', 'Lucía', 'Mejía Rosario', '809-555-1006', 'lucia.mejia@email.com', 'Calle Real #77', 'Moca', 'Laptops, Software', 18000.00, '1995-07-30', 1),
    ('001-7890123-4', 'Francisco', 'Santos Valdez', '809-555-1007', 'fsantos@email.com', 'Av. España #40', 'Puerto Plata', 'Monitores, Periféricos', 14000.00, '1987-12-08', 1),
    ('001-8901234-5', 'Elena', 'Vargas Almonte', '809-555-1008', 'evargas@email.com', 'Calle Mella #19', 'San Francisco', 'Tecnología, Oficina', 8000.00, '1994-04-11', 1),
    ('001-9012345-6', 'Héctor', 'Cruz Batista', '809-555-1009', 'hector.cruz@email.com', 'Av. Libertad #302', 'Bonao', 'Almacenamiento, Redes', 9500.00, '1980-08-25', 1),
    ('001-0123456-7', 'Daniela', 'Reyes Guzmán', '809-555-1010', 'dreyes@email.com', 'Calle Anacaona #9', 'Higüey', 'Laptops, Gaming', 22000.00, '1996-02-17', 1),
    ('001-1122334-8', 'Manuel', 'Cordero Soto', '809-555-1011', 'mcordero@email.com', 'Calle Marina #5', 'La Romana', 'Audio, Periféricos', 11000.00, '1989-06-15', 1),
    ('001-2233445-9', 'Rosa María', 'Núñez Gil', '809-555-1012', 'rosanunez@email.com', 'Av. Central #101', 'San Pedro', 'Impresión, Oficina', 13500.00, '1991-10-05', 1),
    ('001-3344556-0', 'Andrés', 'Paredes Bello', '809-555-1013', 'aparedes@email.com', 'Av. Independencia #54', 'Barahona', 'Accesorios, Redes', 7500.00, '1984-03-29', 1),
    ('001-4455667-1', 'Gabriela', 'López Paulino', '809-555-1014', 'gabi.lopez@email.com', 'Calle Sánchez #18', 'Baní', 'Laptops, Monitores', 20000.00, '1993-12-12', 1),
    ('001-5566778-2', 'Víctor', 'Cabrera Mora', '809-555-1015', 'vcabrera@email.com', 'Av. Sarasota #70', 'Santo Domingo', 'Componentes, SSD', 28000.00, '1986-05-20', 1),
    ('001-6677889-3', 'Beatriz', 'Pérez Collado', '809-555-1016', 'bperez@email.com', 'Av. Abraham Lincoln #412', 'Santo Domingo', 'Software, Oficina', 16000.00, '1990-09-02', 1),
    ('001-7788990-4', 'Esteban', 'Pichardo Ruiz', '809-555-1017', 'epichardo@email.com', 'Calle Restauración #95', 'Santiago', 'Gaming, Audio', 24000.00, '1997-01-18', 1),
    ('001-8899001-5', 'Carmen', 'Herrera Díaz', '809-555-1018', 'cherrera@email.com', 'Av. Estrella Sadhalá #15', 'Santiago', 'Periféricos, Redes', 12500.00, '1982-07-07', 1),
    ('001-9900112-6', 'Alejandro', 'Tejeda Marte', '809-555-1019', 'atejeda@email.com', 'Calle Duarte #60', 'Azua', 'Impresión, Consumibles', 9000.00, '1988-04-24', 1),
    ('001-0011223-7', 'Valeria', 'Solano Mateo', '809-555-1020', 'vsolano@email.com', 'Calle Las Carreras #33', 'San Cristóbal', 'Laptops, Accesorios', 17000.00, '1995-11-28', 1);
    PRINT '20 Clientes insertados.';
END
GO

-- 50 Productos
IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE Codigo = 'LAP-001')
BEGIN
    INSERT INTO dbo.Productos (IdCategoria, Codigo, Nombre, Descripcion, PrecioUnitario, Stock, Activo) VALUES
    -- Categoria 1: Computadoras y Laptops (IdCategoria = 1)
    (1, 'LAP-001', 'Laptop Dell XPS 15 9530', 'Intel Core i9 13th Gen, 32GB RAM, 1TB SSD NVMe, OLED 3.5K', 2199.99, 15, 1),
    (1, 'LAP-002', 'MacBook Pro 16 M3 Pro', 'Apple M3 Pro 12-core CPU, 18GB memoria unificada, 512GB SSD', 2499.00, 12, 1),
    (1, 'LAP-003', 'Lenovo ThinkPad X1 Carbon Gen 11', 'Intel Core i7 1365U, 16GB RAM, 512GB SSD, Pantalla 14 FHD+', 1450.00, 20, 1),
    (1, 'LAP-004', 'Asus ROG Zephyrus G14', 'AMD Ryzen 9 7940HS, RTX 4070, 16GB DDR5, 1TB NVMe, QHD 165Hz', 1799.50, 10, 1),
    (1, 'LAP-005', 'HP Pavilion Desktop Tower', 'Intel Core i5 13400, 16GB DDR4, 512GB SSD, Gráficos UHD 730', 749.99, 25, 1),

    -- Categoria 2: Monitores y Pantallas (IdCategoria = 2)
    (2, 'MON-001', 'Monitor LG UltraGear 27GP850-B', '27 Pulgadas Nano IPS, QHD 2560x1440, 165Hz 1ms, G-Sync Compatible', 349.99, 30, 1),
    (2, 'MON-002', 'Monitor Dell UltraSharp U2723QE', '27 Pulgadas 4K UHD, IPS Black, USB-C Hub 90W PD, 100% sRGB', 589.00, 18, 1),
    (2, 'MON-003', 'Monitor Samsung Odyssey G9 49"', 'Curvo 1000R Dual QHD 5120x1440, 240Hz 1ms, HDR1000', 1199.00, 8, 1),
    (2, 'MON-004', 'Monitor ASUS ProArt PA248QV', '24 Pulgadas 16:10 1920x1200, Calibrado de fábrica Delta E < 2', 199.95, 22, 1),
    (2, 'MON-005', 'Monitor BenQ EW3270U 32" 4K', '32 Pulgadas 4K HDR, Sensor de brillo inteligente B.I.+', 379.50, 14, 1),

    -- Categoria 3: Componentes de Hardware (IdCategoria = 3)
    (3, 'CMP-001', 'Procesador AMD Ryzen 7 7800X3D', '8 núcleos 16 hilos hasta 5.0GHz, 104MB Cache con 3D V-Cache', 399.00, 25, 1),
    (3, 'CMP-002', 'Procesador Intel Core i7-14700K', '20 núcleos (8P + 12E), hasta 5.6GHz, Socket LGA1700', 415.00, 20, 1),
    (3, 'CMP-003', 'Tarjeta Gráfica NVIDIA RTX 4070 Ti Super', '16GB GDDR6X, DLSS 3, Ray Tracing, Triple ventilador', 849.99, 10, 1),
    (3, 'CMP-004', 'Tarjeta Gráfica AMD Radeon RX 7800 XT', '16GB GDDR6, FSR 3, Arquitectura RDNA 3, DisplayPort 2.1', 519.00, 15, 1),
    (3, 'CMP-005', 'Motherboard ASUS ROG Strix B650E-F', 'Socket AM5, PCIe 5.0, WiFi 6E, 4x M.2, Audio SupremeFX', 279.99, 18, 1),
    (3, 'CMP-006', 'Memoria Corsair Vengeance DDR5 32GB (2x16)', '6000MHz CL30 AMD EXPO / Intel XMP 3.0, Color Negro', 119.99, 40, 1),
    (3, 'CMP-007', 'Fuente Corsair RM850x 850W Gold', 'Totalmente modular, Certificación 80 Plus Gold, ATX 3.0', 139.99, 25, 1),
    (3, 'CMP-008', 'Refrigeración Líquida Arctic Liquid Freezer III 360', 'Radiador de 360mm, Ventiladores P12 PWM, Bomba silenciosa', 124.99, 16, 1),

    -- Categoria 4: Almacenamiento Digital (IdCategoria = 4)
    (4, 'ALM-001', 'SSD Samsung 990 PRO 2TB NVMe', 'PCIe 4.0 hasta 7450 MB/s lectura, con disipador térmico', 179.99, 35, 1),
    (4, 'ALM-002', 'SSD Crucial P3 Plus 1TB NVMe', 'PCIe Gen4 x4 M.2 2280, hasta 5000 MB/s lectura secuencial', 69.99, 50, 1),
    (4, 'ALM-003', 'Disco Duro Western Digital Black 4TB', '7200 RPM, SATA 6 Gb/s, 256MB Cache para alto rendimiento', 139.50, 20, 1),
    (4, 'ALM-004', 'SSD Portátil SanDisk Extreme 1TB', 'USB 3.2 Gen 2, hasta 1050 MB/s, Resistencia a caídas IP55', 109.99, 30, 1),
    (4, 'ALM-005', 'SSD Kingston KC3000 2TB PCIe 4.0', 'Velocidad hasta 7000 MB/s, Controlador Phison E18', 159.00, 22, 1),

    -- Categoria 5: Periféricos y Entrada (IdCategoria = 5)
    (5, 'PRF-001', 'Teclado Mecánico Logitech MX Mechanical', 'Switches táctiles silenciosos, Retroiluminación inteligente', 159.99, 28, 1),
    (5, 'PRF-002', 'Mouse Inalámbrico Logitech MX Master 3S', 'Sensor 8000 DPI, Clics silenciosos, Scroll electromagnético MagSpeed', 99.99, 45, 1),
    (5, 'PRF-003', 'Teclado Gamer Razer Huntsman V2', 'Switches ópticos lineales analógicos, Polling rate 8000Hz', 189.00, 14, 1),
    (5, 'PRF-004', 'Mouse Gamer Razer DeathAdder V3 Pro', 'Ultra ligero 63g, Sensor óptico Focus Pro 30K DPI, 2.4GHz', 129.99, 20, 1),
    (5, 'PRF-005', 'Mousepad SteelSeries QcK Heavy XXL', 'Base de goma antideslizante, Tela de microtejido 900x400mm', 29.99, 60, 1),

    -- Categoria 6: Audio y Multimedia (IdCategoria = 6)
    (6, 'AUD-001', 'Auriculares Sony WH-1000XM5', 'Cancelación activa de ruido Noise Cancelling líder, 30h batería', 398.00, 15, 1),
    (6, 'AUD-002', 'Auriculares Bose QuietComfort Ultra', 'Audio espacial inmersivo, Modo consciente, Bluetooth 5.3', 429.00, 10, 1),
    (6, 'AUD-003', 'Micrófono USB Shure MV7+', 'Salida híbrida XLR y USB-C, Panel táctil LED, DSP integrado', 279.00, 16, 1),
    (6, 'AUD-004', 'Altavoces de Estudio Edifier R1280T', 'Potencia 42W RMS, Drivers de 4", Doble entrada RCA analógica', 99.99, 25, 1),
    (6, 'AUD-005', 'Barra de Sonido Creative Stage V2', 'Con subwoofer dedicado 160W pico, Conexión óptica y Bluetooth', 89.99, 18, 1),

    -- Categoria 7: Redes y Conectividad (IdCategoria = 7)
    (7, 'RED-001', 'Router ASUS RT-AX88U Pro WiFi 6', 'Doble banda AX6000, 2 puertos 2.5G, Seguridad AiProtection Pro', 269.99, 14, 1),
    (7, 'RED-002', 'Switch Gigabit TP-Link TL-SG108 8 Puertos', 'Carcasa metálica, Plug & Play, Ahorro de energía Green Ethernet', 24.99, 40, 1),
    (7, 'RED-003', 'Sistema Mesh TP-Link Deco X55 (Pack 3)', 'WiFi 6 AX3000, Cobertura hasta 600m2, Roaming sin interrupciones', 199.99, 12, 1),
    (7, 'RED-004', 'Adaptador USB WiFi 6E TP-Link Archer TX20U', 'Tribanda AXE5400, Antenas de alta ganancia plegables', 49.99, 35, 1),
    (7, 'RED-005', 'Cable de Red Cat 6A Ugreen 15m', 'Velocidad 10Gbps, Blindaje FTP, Conectores RJ45 chapados en oro', 18.50, 75, 1),

    -- Categoria 8: Impresión y Escaneo (IdCategoria = 8)
    (8, 'IMP-001', 'Impresora Epson EcoTank L3250', 'Multifuncional inalámbrica con sistema de tanque de tinta continua', 189.99, 20, 1),
    (8, 'IMP-002', 'Impresora Láser HP LaserJet Pro M404dn', 'Monocromática, Doble cara automática, 40 ppm, Ethernet gigabit', 279.00, 10, 1),
    (8, 'IMP-003', 'Escáner Portátil Brother DS-640', 'Alimentación USB, Velocidad hasta 16 ppm, Escaneo a PDF directo', 119.50, 15, 1),
    (8, 'IMP-004', 'Botella de Tinta Epson Negra T544', 'Rendimiento hasta 4500 páginas, Tinta original resistente al agua', 12.99, 100, 1),
    (8, 'IMP-005', 'Cartucho Toner HP 58A Negro Original', 'Rendimiento aproximado 3000 páginas estándar ISO/IEC', 89.00, 30, 1),

    -- Categoria 9: Accesorios y Cables (IdCategoria = 9)
    (9, 'ACC-001', 'Hub USB-C Anker PowerExpand 8-en-1', 'Salida HDMI 4K@60Hz, Carga 100W PD, Ethernet Gigabit, SD/TF', 59.99, 45, 1),
    (9, 'ACC-002', 'Cable HDMI 2.1 Ultra High Speed 8K 2m', 'Soporta 8K@60Hz, 4K@120Hz, Dynamic HDR, Ancho de banda 48Gbps', 15.99, 80, 1),
    (9, 'ACC-003', 'Soporte de Aluminio para Laptop Nulaxy', 'Ergonómico ajustable en altura y ángulo, Ventilado antideslizante', 27.99, 50, 1),
    (9, 'ACC-004', 'Cargador Rápido Baseus GaN 100W', '4 puertos (2x USB-C + 2x USB-A), Compatible QC 4+ y PD 3.0', 44.99, 35, 1),
    (9, 'ACC-005', 'Webcam Logitech C920x HD Pro', 'Resolución Full HD 1080p a 30fps, Enfoque automático y micrófono dual', 69.99, 30, 1),

    -- Categoria 10: Software y Licencias (IdCategoria = 10)
    (10, 'SFT-001', 'Microsoft Windows 11 Pro 64-Bit', 'Licencia digital OEM para 1 equipo con soporte para BitLocker', 149.00, 50, 1),
    (10, 'SFT-002', 'Microsoft 365 Personal (1 Año)', 'Word, Excel, PowerPoint, Outlook y 1TB de almacenamiento OneDrive', 69.99, 40, 1),
    (10, 'SFT-003', 'Antivirus ESET Internet Security (1 Año/1 PC)', 'Protección multicapa, Banca segura y control parental', 39.99, 35, 1);
    PRINT '50 Productos insertados.';
END
GO

-- 40 Ventas con sus DetalleVenta
IF NOT EXISTS (SELECT 1 FROM dbo.Ventas WHERE NumeroFactura = 'FAC-2026-0001')
BEGIN
    DECLARE @v INT = 1;
    DECLARE @nroFactura NVARCHAR(30);
    DECLARE @idCliente INT;
    DECLARE @idUsuario INT;
    DECLARE @fecha DATETIME;
    DECLARE @subtotal DECIMAL(18,2);
    DECLARE @impuesto DECIMAL(18,2);
    DECLARE @total DECIMAL(18,2);
    DECLARE @idVenta INT;
    DECLARE @prod1 INT, @cant1 INT, @precio1 DECIMAL(18,2);
    DECLARE @prod2 INT, @cant2 INT, @precio2 DECIMAL(18,2);
    DECLARE @metodo NVARCHAR(50);

    WHILE @v <= 40
    BEGIN
        SET @nroFactura = 'FAC-2026-' + RIGHT('0000' + CAST(@v AS NVARCHAR(10)), 4);
        SET @idCliente = ((@v - 1) % 20) + 1;
        SET @idUsuario = ((@v - 1) % 5) + 1;
        SET @fecha = DATEADD(DAY, - (40 - @v) * 2, GETDATE());
        
        SET @prod1 = ((@v * 3) % 45) + 1;
        SET @cant1 = ((@v % 3) + 1);
        SELECT @precio1 = PrecioUnitario FROM dbo.Productos WHERE IdProducto = @prod1;
        IF @precio1 IS NULL SET @precio1 = 150.00;

        SET @prod2 = ((@v * 7) % 45) + 1;
        SET @cant2 = ((@v % 2) + 1);
        SELECT @precio2 = PrecioUnitario FROM dbo.Productos WHERE IdProducto = @prod2;
        IF @precio2 IS NULL SET @precio2 = 45.00;

        SET @subtotal = (@cant1 * @precio1) + (@cant2 * @precio2);
        SET @impuesto = ROUND(@subtotal * 0.18, 2);
        SET @total = @subtotal + @impuesto;

        SET @metodo = CASE (@v % 4)
            WHEN 0 THEN 'Efectivo'
            WHEN 1 THEN 'Tarjeta de Crédito'
            WHEN 2 THEN 'Transferencia Bancaria'
            ELSE 'Tarjeta de Débito'
        END;

        INSERT INTO dbo.Ventas (NumeroFactura, IdCliente, IdUsuario, FechaVenta, Subtotal, Impuesto, Total, MetodoPago, Observaciones, Estado)
        VALUES (@nroFactura, @idCliente, @idUsuario, @fecha, @subtotal, @impuesto, @total, @metodo, 'Venta registrada en demostración', 'Completada');

        SET @idVenta = SCOPE_IDENTITY();

        INSERT INTO dbo.DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario, Subtotal)
        VALUES (@idVenta, @prod1, @cant1, @precio1, (@cant1 * @precio1));

        INSERT INTO dbo.DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario, Subtotal)
        VALUES (@idVenta, @prod2, @cant2, @precio2, (@cant2 * @precio2));

        SET @v = @v + 1;
    END;

    PRINT '40 Ventas y sus detalles insertados exitosamente.';
END
GO

PRINT '=======================================================';
PRINT 'BASE DE DATOS DemoShowcaseDB CONFIGURADA CORRECTAMENTE';
PRINT '=======================================================';
GO
