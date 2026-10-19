# CarniApp — Guía de pruebas (Sprint 10)

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
| `VentaServiceTests`, `CajaServiceTests`, `VentaFacilServiceTests`, `ClienteServiceTests`, `InventarioServiceTests`, `CatalogoServiceTests`, `AuthServiceTests` | Reglas de cada módulo |

Frontend:

```bash
cd carniAppFront
flutter test
```

Ambos se ejecutan solos en GitHub (pestaña **Actions**) con cada `git push`.

## 2. Preparar la base de datos

Corre en `CarniAppDb`, **en orden**, los scripts de `Database/Scripts/` que aún no hayas aplicado
(todos son idempotentes: repetirlos no daña nada):

`001` → `002` → `003` → `004` → `005` → `006` → `007` → `008` → `009` → `010` → `011` → `012` → `013` → `014` → `015`

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

### Roles
16. Entra como **Cajera**: no deben aparecer *Venta Fácil*; en Cobro puede **bajar** el cargo de crédito a 0% pero no subirlo; no puede anular ventas.

### Hardware (cuando lleguen los equipos)
17. Impresora: *Ticket → ⚙ → IP y puerto → Imprimir prueba*, luego imprime un ticket y un corte.
18. Lector de códigos: asigna código de barras a un corte en el catálogo y escanéalo en el POS.
19. Báscula: pendiente de integrar.

## Si algo no cuadra

Anota: pantalla, qué hiciste, qué esperabas y qué salió, y pega el mensaje de error. Con eso se reproduce
como prueba automática antes de corregirlo, para que no vuelva a pasar.
