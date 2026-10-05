# Database schema

```mermaid
erDiagram
    BUSINESS ||--o{ PACKAGE_DEFINITION : offers
    BUSINESS ||--o{ CUSTOMER_PACKAGE : scopes
    BUSINESS ||--o{ TIMETABLE_SCHEDULE : owns
    CUSTOMER ||--o{ CUSTOMER_PACKAGE : owns
    CUSTOMER ||--o{ BOOKING : makes
    CUSTOMER ||--o{ WAITLIST_ENTRY : joins
    PACKAGE_DEFINITION ||--o{ CUSTOMER_PACKAGE : purchased_as
    CUSTOMER_PACKAGE ||--o{ BOOKING : pays_for
    CUSTOMER_PACKAGE ||--o{ WAITLIST_ENTRY : preferred_for
    TIMETABLE_SCHEDULE ||--o{ BOOKING : has
    TIMETABLE_SCHEDULE ||--o{ WAITLIST_ENTRY : queues

    BUSINESS {
        int id PK
        varchar name
    }
    CUSTOMER {
        int id PK
        varchar email UK
        varchar name
    }
    PACKAGE_DEFINITION {
        int id PK
        int business_id FK
        varchar name
        int total_credits
        int validity_days
        decimal price
        bool is_active
    }
    CUSTOMER_PACKAGE {
        bigint id PK
        int customer_id FK
        int package_definition_id FK
        int business_id FK
        int total_credits
        int remaining_credits
        datetime purchased_at_utc
        datetime expires_at_utc
        uint version "optimistic concurrency"
    }
    TIMETABLE_SCHEDULE {
        bigint id PK
        int business_id FK
        varchar class_name
        varchar instructor_name
        datetime start_time_utc
        datetime end_time_utc
        int capacity
        uint version "optimistic concurrency"
    }
    BOOKING {
        bigint id PK
        int customer_id FK
        bigint schedule_id FK
        bigint customer_package_id FK
        enum status
        datetime booked_at_utc
        datetime cancelled_at_utc
        bool credit_refunded
        bigint promoted_from_waitlist_entry_id UK
    }
    WAITLIST_ENTRY {
        bigint id PK
        int customer_id FK
        bigint schedule_id FK
        bigint preferred_customer_package_id FK
        enum status
        datetime joined_at_utc
        datetime resolved_at_utc
        bigint promoted_booking_id
        varchar resolution_reason
    }
```

Important indexes:

- `bookings(schedule_id, status)` makes capacity counts efficient.
- `bookings(customer_id, status)` supports overlap validation.
- `waitlist_entries(schedule_id, status, joined_at_utc)` supports deterministic FIFO promotion.
- `customer_packages(customer_id, business_id, expires_at_utc)` supports package eligibility queries.
- `timetable_schedules(business_id, start_time_utc)` supports business/date filtering.
- `bookings.promoted_from_waitlist_entry_id` is unique, making a waitlist promotion idempotent at the database boundary.

`CustomerPackage.Version` and `TimetableSchedule.Version` are EF concurrency tokens. Credits are snapshotted on purchase so later edits to a package definition do not rewrite a customer's purchase.

