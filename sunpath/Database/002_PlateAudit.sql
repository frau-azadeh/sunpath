-- Read-only audit. VehicleType: 0 passenger car, 1 pickup, 2 truck, 3 motorcycle.
-- Review rows in the UI; never fabricate real plate numbers from legacy values.
SELECT Id, Model, VehicleType, PlateNumber
FROM Vehicles
WHERE VehicleType NOT BETWEEN 0 AND 3
   OR (VehicleType = 3 AND REPLACE(REPLACE(PlateNumber, '-', ''), ' ', '') LIKE N'%[^0-9]%')
   OR (VehicleType = 3 AND LEN(REPLACE(REPLACE(PlateNumber, '-', ''), ' ', '')) <> 8)
ORDER BY Id;
