# SunPath realtime

راهنمای کامل فارسی اجرا، IP/هات‌اسپات، HTTPS گوشی، پلاک خودرو/موتور و شبیه‌سازی در `../../README-FA.md` قرار دارد.

- جریان واقعی: پنل راننده → Geolocation روی HTTPS مورد اعتماد → POST `/api/dispatches/location` → SQL Server و VehicleLocationHistory → VehicleHub → نقشه زنده.
- جریان آزمایشی: کارت مأموریت → POST `/api/simulation/start-mission/{id}` → سرویس مشترک سرور → همان ثبت تاریخچه و انتشار موقعیت → پایان مأموریت در مقصد.
- توقف آزمایشی: DELETE `/api/simulation/stop/{vehicleId}`.
- وضعیت آزمایشی: GET `/api/simulation/status/{vehicleId}`.
- اتصال LAN از فرانت روی `/backend` به بک‌اند منتقل می‌شود. مرورگر گوشی از localhost گوشی استفاده نمی‌کند.
- migration موجود: `Database/001_FleetTracking.sql`؛ audit فقط‌خواندنی پلاک موتور: `Database/002_PlateAudit.sql`.

نقاط شبیه‌سازی در تاریخچه مأموریت آزمایشی ثبت می‌شوند؛ آن را برای آزمایش در مأموریت جداگانه استفاده کنید. هنگام شبیه‌سازی، ثبت GPS از API واقعی همان وسیله رد می‌شود.
