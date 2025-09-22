# PetHero API (.NET 8 Minimal API + EF Core + SQLite)

Backend que reemplaza el mock del frontend Angular manteniendo **contrato 1:1** (rutas, query params y JSON).

## ✨ Funcionalidades clave
- Usuarios y Perfiles
- Guardianes (perfiles públicos con precios, ciudad, tamaños/tipos aceptados)
- Disponibilidad por días (sin solapamientos)
- Mascotas de los Owners
- Reservas (ciclo completo de estados)
- Favoritos
- Chat de mensajes
- Vouchers de pago y Pagos
- Swagger para pruebas (habilitado)

## 🚀 Inicio rápido
```bash
dotnet restore
dotnet tool install --global dotnet-ef   # si no lo tenés
dotnet ef database update                # crea/actualiza pethero.db
dotnet run
```
- La API sirve en **http://localhost:3000**.
- ConnectionStrings:Default = `Data Source=pethero.db`

## 🔌 Endpoints
Listado reconstruido desde `Program.cs`:
- **/availability**
  - `/availability`
  - `/availability/:param`
- **/availability_exceptions**
  - `/availability_exceptions`
- **/bookings**
  - `/bookings`
  - `/bookings/:param`
- **/favorites**
  - `/favorites`
  - `/favorites/:param`
- **/guardians**
  - `/guardians`
  - `/guardians/:param`
- **/messages**
  - `/messages`
  - `/messages/:param`
- **/paymentVouchers**
  - `/paymentVouchers`
  - `/paymentVouchers/:param`
- **/payments**
  - `/payments`
- **/pets**
  - `/pets`
  - `/pets/:param`
- **/profiles**
  - `/profiles`
  - `/profiles/:param`
- **/reviews**
  - `/reviews`
  - `/reviews/:param`
- **/users**
  - `/users`
  - `/users/:param`

## 🧱 Entidades (resumen)
### AvailabilitySlot
- `CreatedAt`: string
- `UpdatedAt`: string?

### Booking
- `DepositPaid`: bool
- `TotalPrice`: decimal?
- `CreatedAt`: string

### Favorite
- `CreatedAt`: string

### Guardian
- `Name`: string?
- `Bio`: string?
- `PricePerNight`: decimal
- `AcceptedTypes`: List<string>
- `AcceptedSizes`: List<string>
- `Photos`: List<string>?
- `AvatarUrl`: string?
- `RatingAvg`: double?
- `RatingCount`: int?
- `City`: string?

### Message
- `CreatedAt`: string
- `BookingId`: string?
- `Status`: string?

### Payment
- `Amount`: decimal
- `CreatedAt`: string

### PaymentVoucher
- `Amount`: decimal
- `CreatedAt`: string?

### Pet
- `Breed`: string?
- `PhotoUrl`: string?
- `VaccineCalendarUrl`: string?
- `Notes`: string?

### Profile
- `Id`: int
- `UserId`: int
- `Phone`: string?
- `Location`: string?
- `Bio`: string?
- `AvatarUrl`: string?

### Review
- `Rating`: int
- `Comment`: string?
- `CreatedAt`: string

### User
- `Id`: int
- `Password`: string?
- `ProfileId`: int?
- `CreatedAt`: string


## 🧺 DTOs
### AvailabilitySlotDto
- `Id`: string?
- `GuardianId`: string?
- `Start`: string?
- `End`: string?
- `CreatedAt`: string?
- `UpdatedAt`: string?

### BookingDto
- `Id`: string?
- `OwnerId`: string?
- `GuardianId`: string?
- `PetId`: string?
- `Start`: string?
- `End`: string?
- `Status`: string?
- `DepositPaid`: bool?
- `TotalPrice`: decimal?
- `CreatedAt`: string?

### FavoriteDto
- `Id`: string?
- `OwnerId`: string?
- `GuardianId`: string?
- `CreatedAt`: string?

### GuardianDto
- `Id`: string?
- `Name`: string?
- `Bio`: string?
- `PricePerNight`: decimal?
- `AcceptedTypes`: List<string>?
- `AcceptedSizes`: List<string>?
- `Photos`: List<string>?
- `AvatarUrl`: string?
- `RatingAvg`: double?
- `RatingCount`: int?
- `City`: string?

### MessageDto
- `Id`: string?
- `FromUserId`: string?
- `ToUserId`: string?
- `Body`: string?
- `CreatedAt`: string?
- `BookingId`: string?
- `Status`: string?

### PaymentDto
- `Id`: string?
- `BookingId`: string?
- `Amount`: decimal?
- `Type`: string?
- `Status`: string?
- `CreatedAt`: string?

### PaymentVoucherDto
- `Id`: string?
- `BookingId`: string?
- `Amount`: decimal?
- `DueDate`: string?
- `Status`: string?
- `CreatedAt`: string?

### PetDto
- `Id`: string?
- `OwnerId`: string?
- `Name`: string?
- `Type`: string?
- `Breed`: string?
- `Size`: string?
- `PhotoUrl`: string?
- `VaccineCalendarUrl`: string?
- `Notes`: string?

### ReviewDto
- `Id`: string?
- `BookingId`: string?
- `OwnerId`: string?
- `GuardianId`: string?
- `Rating`: int?
- `Comment`: string?
- `CreatedAt`: string?


## 🌱 Seed de datos
Se cargan registros de ejemplo (usuarios, guardianes, etc.) la primera vez para facilitar pruebas desde el frontend.

## 🛠️ Notas de compatibilidad con el frontend
- Rutas **sin prefijo `/api`**.
- Endpoints que usan query params (`?ownerId=`, `?guardianId=`, etc.) devuelven **arrays**.
- `Availability` expone **`start`** y **`end`** (ISO-8601).

## 🧩 Estructura
- `Program.cs` (rutas, CORS, Swagger, migración)
- `Data/PetHeroDbContext.cs` (DbContext)
- `Data/SeedData.cs` (datos de ejemplo)
- `Entities/` (modelo persistente)
- `Dtos/` (contratos de entrada/salida)
- `Migrations/` (migraciones EF Core)

## 🛠️ Troubleshooting
- **CORS**: agregá el origen del frontend si cambia el puerto.
- **DB**: borrar `pethero.db` y re-aplicar migraciones si el esquema cambió.
- **Puerto 3000 ocupado**: ajustar `UseUrls` en `Program.cs`.