# Changelog

## [0.8.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.7.0...v0.8.0) (2026-08-27)


### Features

* **booking:** implements initial domain events ([#31](https://github.com/merg8511/Bookify.Services.Booking/issues/31)) ([b3d2618](https://github.com/merg8511/Bookify.Services.Booking/commit/b3d2618e8c2f1f4b77a42db5299f020edf06fff7))
* **payments:** harden payment flow and project quality gates ([#35](https://github.com/merg8511/Bookify.Services.Booking/issues/35)) ([fe7da2c](https://github.com/merg8511/Bookify.Services.Booking/commit/fe7da2c0a3124c19e99157f52d7ad9a305a2c961))
* **payments:** implements payments abstraction and stripe configuration ([#33](https://github.com/merg8511/Bookify.Services.Booking/issues/33)) ([b161329](https://github.com/merg8511/Bookify.Services.Booking/commit/b161329a7e9e8ee7d9ea621aa46f96832c458b33))
* **payments:** initial payments configuration handlers ([#34](https://github.com/merg8511/Bookify.Services.Booking/issues/34)) ([4d6be77](https://github.com/merg8511/Bookify.Services.Booking/commit/4d6be777250b57b67ef437675c295f6e791b5636))

## [0.7.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.6.0...v0.7.0) (2026-08-21)


### Features

* **api:** implement cross-cutting idempotency key mechanism for api requests ([#27](https://github.com/merg8511/Bookify.Services.Booking/issues/27)) ([a957e03](https://github.com/merg8511/Bookify.Services.Booking/commit/a957e03e0781c3179dc8236e46fc57238edec7a4))
* **booking:** implement booking lifecycle end to end ([#30](https://github.com/merg8511/Bookify.Services.Booking/issues/30)) ([15fb8ed](https://github.com/merg8511/Bookify.Services.Booking/commit/15fb8ed93636681f07b47029da5ac3e7f3fc0a46))
* **infrastructure:** implement pessimistic locking to prevent double booking race conditions ([#25](https://github.com/merg8511/Bookify.Services.Booking/issues/25)) ([aee1c0f](https://github.com/merg8511/Bookify.Services.Booking/commit/aee1c0f58dd504150549fc3b4255143b0f83c3a0))
* **pricing:** implement end-to-end booking pricing flow ([#29](https://github.com/merg8511/Bookify.Services.Booking/issues/29)) ([4cc4105](https://github.com/merg8511/Bookify.Services.Booking/commit/4cc41057fffa97a11fed9c00a6a922382a7bb2cf))


### Bug Fixes

* **code:** review idempotency flow ([#28](https://github.com/merg8511/Bookify.Services.Booking/issues/28)) ([28755ea](https://github.com/merg8511/Bookify.Services.Booking/commit/28755ea1e73e7a72ee4d650a5598c38396be0693))

## [0.6.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.5.0...v0.6.0) (2026-08-07)


### Features

* **api:** add integral availability query endpoint ([#20](https://github.com/merg8511/Bookify.Services.Booking/issues/20)) ([ae115b0](https://github.com/merg8511/Bookify.Services.Booking/commit/ae115b03e0a7e7bb74929d76ae17205b5cfee1cc))
* **api:** implement create booking flow with validation, availability check and atomic persistence ([#23](https://github.com/merg8511/Bookify.Services.Booking/issues/23)) ([#24](https://github.com/merg8511/Bookify.Services.Booking/issues/24)) ([047dc4f](https://github.com/merg8511/Bookify.Services.Booking/commit/047dc4fcbdb9ae5e2a5233e7fae994eef35e3129))

## [0.5.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.4.0...v0.5.0) (2026-08-04)


### Features

* **infrastructure:** filter availability candidates by shared inventory ([#17](https://github.com/merg8511/Bookify.Services.Booking/issues/17)) ([d3a3418](https://github.com/merg8511/Bookify.Services.Booking/commit/d3a3418aab6328ef410ac9b4e9a0d9fe96deaeed))
* **infrastructure:** filter availability conflicts by blocking booking statuses ([6573375](https://github.com/merg8511/Bookify.Services.Booking/commit/6573375c8ff6ee9d28a1c54d3f07c2ebc2c8a686))
* **infrastructure:** implement Dapper query for overlapping bookings ([#15](https://github.com/merg8511/Bookify.Services.Booking/issues/15)) ([3a67dea](https://github.com/merg8511/Bookify.Services.Booking/commit/3a67dea6319350c6a043d5a0e6a86c6e3f2fe8d0))

## [0.4.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.3.0...v0.4.0) (2026-07-31)


### Features

* **api:** add pagination and filtering to the properties endpoint ([af8b2f0](https://github.com/merg8511/Bookify.Services.Booking/commit/af8b2f0540a701c32fbc82c3a31eba40e0b428dc))
* **api:** add safe dynamic sorting to property queries ([#13](https://github.com/merg8511/Bookify.Services.Booking/issues/13)) ([6274280](https://github.com/merg8511/Bookify.Services.Booking/commit/62742803785d02c2f3cbd527f3ca6603c8fbcbcb))


### Bug Fixes

* **application:** guard pagination invariants and correct validation messages ([af8b2f0](https://github.com/merg8511/Bookify.Services.Booking/commit/af8b2f0540a701c32fbc82c3a31eba40e0b428dc))
* **infrastructure:** escape SQL wildcard characters in property name filters ([af8b2f0](https://github.com/merg8511/Bookify.Services.Booking/commit/af8b2f0540a701c32fbc82c3a31eba40e0b428dc))

## [0.3.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.2.0...v0.3.0) (2026-07-28)


### Features

* **infrastructure:** implement Dapper read services for booking queries ([#8](https://github.com/merg8511/Bookify.Services.Booking/issues/8)) ([dd6ff20](https://github.com/merg8511/Bookify.Services.Booking/commit/dd6ff2058249de24a8dafe1696c133e7d84682d5))

## [0.2.0](https://github.com/merg8511/Bookify.Services.Booking/compare/v0.1.0...v0.2.0) (2026-07-27)


### Features

* **infrastructure:** add Dapper type handlers for DateOnly and TimeOnly ([#5](https://github.com/merg8511/Bookify.Services.Booking/issues/5)) ([e3d6f30](https://github.com/merg8511/Bookify.Services.Booking/commit/e3d6f30a3442be8bb715e72ab0b250465fa27902))

## 0.1.0 (2026-07-27)


### Bug Fixes

* **ci:** update existing PR summary comment on new pushes ([474edf1](https://github.com/merg8511/Bookify.Services.Booking/commit/474edf1813e737cd38feb8900e4fe3cb710a6c24))
* **ci:** update gemini model to gemini-1.5-flash in pr summary workflow ([a916b23](https://github.com/merg8511/Bookify.Services.Booking/commit/a916b231f45cfb55ad73733a4351957ce05a2633))
* **ci:** update gemini model to gemini-2.0-flash in pr summary workflow ([6e25a07](https://github.com/merg8511/Bookify.Services.Booking/commit/6e25a070a10c1b696311bfe89a447ec724ceee25))
* **ci:** update gemini model to gemini-2.5-flash in pr summary workflow ([df4745e](https://github.com/merg8511/Bookify.Services.Booking/commit/df4745e83ecaffc97c22aa933e764453661614e1))
* **ci:** update gemini model to gemini-3.6-flash in pr summary workflow ([c613577](https://github.com/merg8511/Bookify.Services.Booking/commit/c613577ed3244e96be65926bcfa4eeec9458236f))
