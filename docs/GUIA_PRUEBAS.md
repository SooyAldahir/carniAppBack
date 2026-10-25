# CarniApp — Guía de pruebas (Sprint 10 y ajustes para producción)

Sirve para validar el sistema completo antes de pasar a producción. Tiene tres partes:
pruebas automáticas, preparación de la base y un recorrido manual con números exactos.

## 1. Pruebas automáticas

Backend (no necesita SQL Server, usa SQLite en memoria):

```bash
cd carniAppBack
dotnet test
```

Qué cubren:

| Archivo | Qué protege |
|---|---|
| `SchemaContractTests` | Cada entidad de EF existe en los scripts SQL con todas sus columnas |
| `ContratoApiTests` | Todo endpoint pide sesión; toda escritura pide un permiso; los permisos usados existen en los scripts y los recibe el Super Administrador; la Cajera tiene lo necesario y nada de administración; todos los servicios están registrados en DI |
| `FlujoIntegralTests` | Un día completo: caja → lotes → ventas (efectivo, mixta, crédito) → abono → gasto → anulación → corte → reabrir. Stock = kardex, saldo del cliente y corte cuadran |
| `VentaServiceTests`, `CajaServiceTests` (incluye meta de efectivo y retiros), `VentaFacilServiceTests`, `ClienteServiceTests`, `InventarioServiceTests`, `CatalogoServiceTests`, `AuthServiceTests` | Reglas de cada módulo |

Frontend:

```bash
cd carniAppFront
flutter pub get
flutter test
```

Qué cubre el front: `caja_test` (turnos, meta de efectivo, retiros y cuándo avisar), `nota_cliente_test`
(dirección y referencia en la nota sin perder lo escrito a mano), `ajustes_test` (color de marca, datos del
negocio y tema), `shell_test` (barra lateral por permisos, cajón en pantallas angostas, cambio de módulo) y
las pruebas de impresión, ticket, clientes y Venta Fácil.

Ambos se ejecutan solos en GitHub (pestaña **Actions**) con cada `git push`.

## 2. Preparar la base de datos

Corre en `CarniAppDb`, **en orden**, los scripts de `Database/Scripts/` que aún no hayas aplicado
(todos son idempotentes: repetirlos no daña nada):

`001` → `002` → `003` → `004` → `005` → `006` → `007` → `008` → `009` → `010` → `011` → `012` → `013` → `014` → `015` → `016`

`016` agrega la meta de efectivo y los retiros de caja, la nota de venta de 500 caracteres y los datos de marca (color y pie del ticket).

Después reinicia la API y **vuelve a iniciar sesión** (los permisos viajan dentro del token).

Roles de fábrica:

| Rol | Puede |
|---|---|
| Super Administrador | Todo |
| Cajera | Vender, descuento básico, bajar el cargo de crédito, clientes y abonos, consultar inventario, abrir/cerrar caja |
| Cajera **no** puede | Anular ventas, descuentos altos, subir el cargo de crédito, ajustar inventario, registrar lotes de animal, editar catálogo, reabrir cortes |

## 3. Recorrido manual (≈ 20 minutos)

Usa un usuario Super Administrador. Los números deben salir **exactamente** así.

### Preparación
1. Catálogo: tipo de animal **Res** con dos cortes por peso: **Bistec $200/kg** y **Molida $120/kg**.
2. Clientes: crea **Don Pepe** con dirección y **límite de crédito $5,000**.

### Caja y lotes
3. **Caja y corte** → abrir caja con fondo **$500**.
4. **Venta Fácil**: registra **3 lotes de Res** de 100 kg, costo $5,000, activando *Pesar cortes*: Bistec 30 kg, Molida 50 kg.
   - Inventario: Bistec **90 kg**, Molida **150 kg**.
5. Registra un **4.º lote de 200 kg sin pesar**: debe decir *Calculado con lo aprendido de 3 lotes pesados*.
   - Inventario: Bistec **150 kg**, Molida **250 kg**.

### Ventas
6. **Venta 1**: 5 kg de Bistec, efectivo → **$1,000**.
7. **Venta 2**: 2 kg de Molida + 1 kg de Bistec, pago mixto **$140 efectivo + $300 tarjeta** → $440.
8. **Venta 3**: 10 kg de Molida a **Don Pepe**, crédito → ticket con *Cargo por crédito (5%) +$60* y **total $1,260**.
9. **Venta 4**: 1 kg de Bistec en efectivo y **anúlala** con un motivo.
   - Inventario: Bistec **144 kg**, Molida **238 kg**.
10. Clientes → Don Pepe → **abono de $500** → saldo **$760**.
11. Caja → **Registrar gasto**: Hielo, **$100**.

### Corte
12. Caja → **Cerrar caja**. Debe mostrar *esperado* **$2,040**. Cuenta **$2,000** → **Faltante $40**.
    - Ventas: 3 · Total **$2,700** (Efectivo $1,140 · Tarjeta $300 · Crédito $1,260) · 1 anulada.
13. Intenta anular la **Venta 1** → debe rechazarlo (*pertenece a un corte cerrado*).
14. Historial de cortes → ese corte → **Reabrir** → ahora sí anula la Venta 1 (Bistec vuelve a **149 kg**).
15. Cierra otra vez contando **$1,040** → **Cuadra**, total de ventas **$1,700**.

### Nota del ticket, cobro y meta de caja (script 016)
Con el turno del paso 15 cerrado, abre uno nuevo y sigue estos pasos.

16. **Caja y corte** → abrir caja con fondo **$500** y **meta de efectivo $1,000**. Debe verse la barra *En caja $500.00 de $1,000.00*.
17. **Punto de venta** → agrega 1 kg de Bistec → **Elegir cliente** → Don Pepe: en *nota* aparece solo su **dirección** (y referencia si la capturaste), con el prefijo `Dir:` / `Ref:`.
18. Toca la nota y agrega una línea (por ejemplo *Llevar cambio de $500*): se conservan las dos cosas. Cambia a otro cliente: la dirección cambia y tu línea sigue ahí. Quita al cliente (mostrador): se va la dirección y queda tu línea.
19. Con Don Pepe otra vez, toca **Cobrar** (botón sin relleno): registra la venta con el flujo normal y **no imprime**. Con otra venta toca **Cobrar e imprimir**: registra y manda el ticket; si no hay impresora configurada dice *La venta quedó registrada, pero no se pudo imprimir* y ofrece **Imprimir** para reintentar.
20. En el ticket (*Ver ticket*) la dirección sale **una sola vez** y la línea extra aparece en la nota. Si definiste *Mensaje al pie del ticket* en Ajustes, sale al final.
21. Cobra en efectivo hasta que el efectivo en caja llegue a **$1,000**: al terminar el cobro aparece la ventana **Hay que hacer un retiro**. El importe sugerido es lo que pasa del fondo (con **$1,500** en caja, sugiere **$1,000.00**). *Más tarde* la cierra y no vuelve a salir hasta que el efectivo suba otro 20 % de la meta; *Registrar retiro* descuenta el dinero.
22. **Caja y corte** → el corte muestra *Retiros de efectivo* aparte de los gastos y el *Efectivo esperado* ya los descuenta. **Retiro de efectivo** también se puede hacer a mano en cualquier momento, y **Cambiar** meta la modifica o la quita.

### Interfaz y personalización
23. Tras iniciar sesión se ve la **barra lateral** con los módulos por sección (Operación, Gestión, Sistema) y **Inicio** con las ventas de hoy, la caja con su meta y las existencias bajas. Cambiar de módulo **no** pierde el ticket abierto del punto de venta.
24. Contrae y expande el menú con las flechas de abajo; en una ventana angosta (menos de 860 px) el menú pasa a un cajón con el botón de las tres rayas.
25. **Ajustes** → *Negocio*: cambia el nombre, el teléfono y el pie del ticket y guarda; la barra lateral y el ticket lo reflejan. Solo quien tiene `config.critica.editar` puede editar (la Cajera lo ve sin poder cambiarlo).
26. **Ajustes** → *Apariencia*: elige otro color de la marca (o escribe uno `#RRGGBB`): botones y selección cambian en todas las cajas. *Modo de pantalla* (Sistema / Claro / Oscuro) y *Abrir el cajón al cobrar en efectivo* se guardan solo en ese equipo.

### Roles
27. Entra como **Cajera**: no deben aparecer *Venta Fácil*; en Cobro puede **bajar** el cargo de crédito a 0% pero no subirlo; no puede anular ventas.

### Hardware (cuando lleguen los equipos)
28. Impresora: *Ajustes → Impresión de tickets → Configurar* (red, Bluetooth, USB o cola del sistema) → *Imprimir prueba*, luego imprime un ticket y un corte.
29. Lector de códigos: asigna código de barras a un corte en el catálogo y escanéalo en el POS.
30. Báscula: pendiente de integrar.

## Si algo no cuadra

Anota: pantalla, qué hiciste, qué esperabas y qué salió, y pega el mensaje de error. Con eso se reproduce
como prueba automática antes de corregirlo, para que no vuelva a pasar.
