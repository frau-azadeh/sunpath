# SunPath - Real Fleet Tracking

این نسخه دیگر برای حرکت خودرو از `VehicleSimulationService` یا `Math.random()` استفاده نمی‌کند.

## جریان واقعی

1. مدیر از بخش رانندگان، راننده را با نام کاربری و رمز عبور ثبت می‌کند.
2. مدیر خودرو را ثبت می‌کند.
3. مدیر در بخش مأموریت، راننده + خودرو + مبدأ + مقصد را انتخاب می‌کند.
4. مأموریت در جدول `Missions` ذخیره می‌شود و `Vehicle.CurrentDriverId` نیز به‌روزرسانی می‌شود.
5. راننده با نام کاربری و رمز عبور وارد `/driver/login` می‌شود.
6. پنل راننده مأموریت فعال را از `/api/dispatches/driver/{driverId}/active` می‌گیرد.
7. با شروع مأموریت، `navigator.geolocation.watchPosition` مختصات واقعی گوشی را می‌گیرد.
8. هر آپدیت به `/api/dispatches/location` ارسال و در `VehicleLocationHistory` ذخیره می‌شود.
9. Backend همان آپدیت را از `VehicleHub` به گروه `live-map` می‌فرستد.
10. داشبورد و `/map` بدون refresh موقعیت خودرو را روی نقشه به‌روزرسانی می‌کنند.
11. مصرف سوخت تخمینی سمت سرور از مسافت واقعی GPS + زمان توقف محاسبه و همراه telemetry منتشر می‌شود.

## نصب دیتابیس

قبل از اجرای Backend فایل زیر را روی دیتابیس `SunPathDb` اجرا کن:

`Database/001_FleetTracking.sql`

## نکته مهم GPS

برای GPS واقعی مرورگر، صفحه راننده باید در HTTPS اجرا شود (یا localhost در توسعه). روی موبایل، مجوز Location باید به مرورگر داده شود.

## حذف شبیه‌سازی

ثبت `VehicleSimulationService` از `Startup` حذف شده و فایل‌های Simulation و MissionDispatch قدیمی حذف شده‌اند تا هیچ حرکت ساختگی در مسیر واقعی وارد سیستم نشود.

## API های اصلی

- `GET /api/drivers`
- `POST /api/drivers`
- `PUT /api/drivers/{id}`
- `DELETE /api/drivers/{id}`
- `POST /api/driverauth/login`
- `GET /api/vehicles`
- `POST /api/vehicles`
- `PUT /api/vehicles/{id}`
- `DELETE /api/vehicles/{id}`
- `GET /api/dispatches`
- `GET /api/dispatches/driver/{driverId}/active`
- `POST /api/dispatches`
- `PUT /api/dispatches/{id}`
- `PATCH /api/dispatches/{id}/status`
- `POST /api/dispatches/location`
- SignalR: `/vehicleHub`

رمز عبور راننده به‌صورت PBKDF2 ذخیره می‌شود و در API لیست رانندگان برگردانده نمی‌شود.
