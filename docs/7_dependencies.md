7. Внутренние зависимости
Внутренние зависимости определяют взаимодействие проектов внутри решения.
Api
  ↓
 Registrations
  ↓
 Contracts
Api
  ↓
 Events
  ↓
 Contracts
Api
  ↓
 Tickets
  ↓
 Contracts
Api
  ↓
 Users
  ↓
 Contracts

При этом бизнес-модули не должны напрямую обращаться к внутренним классам друг друга.
Взаимодействие выполняется через публичные контракты.
Например:
RegistrationModule
      ↓
IEventCapacityService
      ↓
EventModule
Это позволяет уменьшить связанность модулей.