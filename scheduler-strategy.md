# Background Job Scheduler Strategy

**Status:** Accepted  
**Date:** 2026-10-10  
**Module:** 18.10 — Hangfire / Quartz

## Context

Bookify currently requires background processing for:

- approval expiration;
- payment expiration and payment-provider reconciliation;
- automatic booking completion;
- transactional Outbox processing;
- booking notifications;
- payment deadline reminders;
- upcoming check-in reminders.

During Module 18, Bookify implemented these capabilities using:

- ASP.NET Core `BackgroundService`;
- configurable polling;
- scoped job execution;
- bounded batch processing;
- `CancellationToken` propagation;
- graceful shutdown;
- PostgreSQL candidate selection;
- PostgreSQL advisory claims for multi-instance coordination;
- expected-state revalidation before mutations;
- transactional Outbox persistence;
- durable notification delivery keys.

The current scheduling model is state-driven.

Jobs do not primarily execute because a calendar expression says that a particular method must run at a particular clock time. Instead, each polling cycle asks PostgreSQL whether durable business state contains work that is currently due.

Examples:

- a booking whose approval deadline has expired;
- a booking whose payment deadline has expired;
- a paid booking whose checkout instant has passed;
- an unprocessed Outbox message;
- a payment deadline entering its reminder window;
- a paid booking entering its check-in reminder window.

## Decision

For the current Bookify scope, keep:

**ASP.NET Core `BackgroundService` + PostgreSQL coordination.**

Do not introduce Hangfire or Quartz at this stage.

The existing implementation already provides the capabilities currently required by the Booking service without introducing an additional scheduler persistence model.

This decision is intentional and final for the current scope. Hangfire and Quartz are migration options only if future requirements meet one or more of the criteria defined below.

## Why the current architecture is sufficient

### Durable source of work

Business work is already reconstructable from PostgreSQL.

Booking deadlines, booking states, Outbox messages and notification delivery records survive application restarts.

The scheduler itself therefore does not need to persist a separate copy of every future execution.

### Multi-instance coordination

PostgreSQL advisory claims prevent multiple application instances from concurrently processing the same job work item.

The claim is scoped by job type and work item identifier, allowing different work items and different job types to execute independently.

### Idempotency

Handlers revalidate expected business state before performing mutations.

Notification delivery uses a durable `BookingId + NotificationType` delivery key.

Outbox messages retain their processed state.

Retries therefore operate against durable state instead of depending on process memory.

### Failure recovery

If a process terminates, durable candidate state remains in PostgreSQL and becomes eligible again on a later polling cycle.

Session-level PostgreSQL advisory claims are released when their database session terminates.

### Operational simplicity

The service currently has a small, known collection of background jobs that share the same polling infrastructure.

Adding a scheduler framework now would introduce additional packages, configuration, persistence concepts and operational surface without replacing a missing capability.

## Migration criteria

The current architecture should be reconsidered if one or more of the following requirements becomes material.

### 1. Complex cron or calendar scheduling

Consider migrating when jobs must be scheduled primarily through calendar expressions rather than by querying durable business state.

Examples include:

- weekday-only schedules;
- monthly or annual schedules;
- multiple independent cron expressions;
- holiday calendars;
- user-configurable schedules;
- explicit misfire policies;
- large numbers of timezone-specific schedules.

Quartz should be evaluated first when scheduling complexity is the principal requirement.

### 2. Operational job dashboard

Consider migrating when operators require a dedicated UI to:

- inspect queued jobs;
- inspect execution history;
- inspect failures;
- manually retry jobs;
- trigger jobs manually;
- pause or resume processing;
- inspect queues or workers.

Hangfire should be evaluated first when an operational dashboard and interactive job administration are primary requirements.

Bookify observability through logging, tracing and metrics remains a separate concern and is implemented in Module 19.

### 3. Persistent scheduling as first-class data

Consider migrating when the schedule itself must survive independently of Bookify business entities.

Examples include:

- execute a specific command at a future timestamp;
- dynamically create or delete schedules;
- reschedule individual executions;
- retain scheduler trigger state;
- manage misfires after long downtime.

This is different from the current model, where PostgreSQL business state can reconstruct whether work is due after a restart.

Quartz persistent stores should be evaluated when persistent trigger semantics and calendar scheduling are dominant.

Hangfire should be evaluated when durable fire-and-forget, delayed and recurring job execution is the dominant requirement.

### 4. Advanced retry policies

Consider migrating when “retry on the next polling cycle” is no longer sufficient and jobs require scheduler-managed policies such as:

- per-job retry counts;
- exponential or custom backoff;
- scheduled retry timestamps;
- failed-job administration;
- manual retries;
- dead-letter-style workflows;
- different retry policies for different classes of jobs.

Hangfire should be evaluated first when automatic retry management and operational failed-job handling are primary requirements.

Retry or resilience around outbound dependencies may also remain an application concern depending on the failure being handled.

### 5. Large or heterogeneous job catalog

Reconsider the scheduler when background work stops behaving like one bounded family of polling jobs and instead requires:

- independent schedules;
- independent worker pools;
- queue priorities;
- different concurrency limits;
- different execution windows;
- separate operational ownership;
- runtime registration of job types.

The trigger is not a specific numeric count of job classes.

The trigger is architectural: adding a new job should no longer fit naturally into the existing `IBackgroundJob` polling model without adding scheduler behavior to Bookify itself.

## Hangfire vs Quartz if migration becomes necessary

The migration target must be selected from the requirement that caused the migration.

### Prefer evaluating Hangfire when

The dominant requirements are:

- operational dashboard;
- straightforward background method execution;
- persistent fire-and-forget or delayed jobs;
- recurring jobs;
- automatic retries;
- queue-oriented job processing;
- manual operational retry and inspection.

For PostgreSQL deployments, the PostgreSQL storage provider must also be evaluated as an additional dependency.

### Prefer evaluating Quartz when

The dominant requirements are:

- rich cron schedules;
- calendar-driven execution;
- persistent trigger state;
- misfire semantics;
- scheduler clustering;
- complex scheduling rules;
- many independently scheduled recurring jobs.

## Non-goals of the current architecture

The current Bookify background infrastructure does not attempt to provide:

- a general-purpose scheduler platform;
- an administrative jobs dashboard;
- arbitrary user-defined cron expressions;
- arbitrary future one-off command scheduling;
- distributed transactions with external providers;
- exactly-once SMTP delivery.

These capabilities should not be added until a concrete business or operational requirement requires them.

## Re-evaluation process

When a migration criterion is reached:

1. identify the requirement that the current model cannot satisfy cleanly;
2. measure the operational or implementation cost of extending the current model;
3. compare Hangfire and Quartz specifically against that requirement;
4. evaluate PostgreSQL storage and deployment implications;
5. define migration of existing `IBackgroundJob` implementations;
6. preserve domain and application idempotency even if scheduling infrastructure changes;
7. create the migration as a dedicated roadmap item rather than introducing it incidentally.

## Consequences

### Positive

- no new scheduler dependency;
- no duplicate scheduling persistence;
- no new scheduler database schema;
- existing PostgreSQL remains the coordination source;
- current jobs remain simple and explicit;
- domain/application logic stays independent of scheduler technology;
- future migration remains possible because jobs already expose application-level execution boundaries.

### Trade-offs

Bookify does not currently receive:

- a dedicated background-job dashboard;
- framework-managed advanced retries;
- arbitrary persistent schedules;
- rich cron/calendar scheduling;
- scheduler-native job history.

Those trade-offs are accepted for the current scope.

## Final decision

**Bookify remains on `BackgroundService + PostgreSQL coordination` for the current architecture.**

Hangfire and Quartz are not installed in Module 18.

They will only be reconsidered when a concrete migration criterion in this document is met.
