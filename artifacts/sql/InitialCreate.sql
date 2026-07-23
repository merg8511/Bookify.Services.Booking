CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE properties (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    time_zone_id character varying(100) NOT NULL,
    check_in_time time without time zone NOT NULL,
    check_out_time time without time zone NOT NULL,
    is_active boolean NOT NULL,
    CONSTRAINT pk_properties PRIMARY KEY (id)
);

CREATE TABLE rentable_units (
    id uuid NOT NULL,
    property_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    type character varying(32) NOT NULL,
    maximum_capacity integer NOT NULL,
    max_base_guests integer NOT NULL,
    is_active boolean NOT NULL,
    CONSTRAINT pk_rentable_units PRIMARY KEY (id),
    CONSTRAINT ck_rentable_units_base_guests_capacity CHECK (max_base_guests <= maximum_capacity),
    CONSTRAINT ck_rentable_units_maximum_capacity CHECK (maximum_capacity > 0),
    CONSTRAINT ck_rentable_units_type CHECK (type IN ('EntireProperty', 'Room')),
    CONSTRAINT fk_rentable_units_properties_id FOREIGN KEY (property_id) REFERENCES properties (id) ON DELETE RESTRICT
);

CREATE TABLE bookings (
    id uuid NOT NULL,
    property_id uuid NOT NULL,
    rentable_unit_id uuid NOT NULL,
    check_in_date date NOT NULL,
    check_out_date date NOT NULL,
    guest_count integer NOT NULL,
    status character varying(32) NOT NULL,
    cancellation_reason character varying(32),
    CONSTRAINT pk_bookings PRIMARY KEY (id),
    CONSTRAINT ck_bookgins_stay_period CHECK (check_out_date > check_in_date),
    CONSTRAINT ck_bookings_cancellation_consistency CHECK ((status = 'Cancelled' AND cancellation_reason IS NOT NULL) OR (status <> 'Cancelled' AND cancellation_reason IS NULL)),
    CONSTRAINT ck_bookings_guest_count CHECK (guest_count > 0),
    CONSTRAINT ck_bookings_status CHECK (status IN ('PendingApproval', 'PendingPayment', 'Paid', 'Completed', 'Cancelled')),
    CONSTRAINT fk_bookings_properties_property_id FOREIGN KEY (property_id) REFERENCES properties (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bookings_rentable_units_rentable_unit_id FOREIGN KEY (rentable_unit_id) REFERENCES rentable_units (id) ON DELETE RESTRICT
);

CREATE INDEX ix_bookings_check_in_date_check_out_date ON bookings (check_in_date, check_out_date);

CREATE INDEX ix_bookings_property_id_status ON bookings (property_id, status);

CREATE INDEX ix_bookings_rentable_unit_id_status ON bookings (rentable_unit_id, status);

CREATE INDEX ix_rentable_units_property_id_type ON rentable_units (property_id, type);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260723012919_InitialCreate', '10.0.4');

COMMIT;

