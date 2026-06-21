# ADR-0006: gRPC antar-service, REST untuk UI

> Architecture Decision Record. Immutable setelah Accepted (perubahan = ADR baru yang men-*supersede*). `→ Canon: Richards & Ford (Fundamentals), ADRs; format ala Nygard.`

- **Status:** Accepted
- **Tanggal:** 2026-06-20
- **Pengambil keputusan:** Tom (solo architect)
- **Konteks teknis:** `<Module>.Api` (gRPC service + REST endpoint), `<Module>.Grpc`, Gateway, WebUI

## Context

Ada dua kelas komunikasi sinkron yang berbeda kebutuhannya: (a) **service-to-service** internal — read API master data/auth ([ADR-0011](0011-master-data-read-api-cache-aside.md)), dipanggil sering, butuh kontrak ketat & efisien; (b) **client-to-service** dari WebUI/eksternal — butuh kompatibilitas web, mudah di-debug, ramah gateway.

## Decision

- **Pilihan:** **gRPC** untuk komunikasi **antar-service** (HTTP/2, kontrak `.proto`); **REST** di-expose tiap `<Module>.Api` untuk **UI & gateway**. Gateway (APIM/Apigee managed; YARP lokal) cukup melakukan **routing + cross-cutting** (auth, rate-limit, TLS) — **tanpa transcoding** karena service sudah expose REST sendiri.
- **Kenapa:** gRPC unggul untuk komunikasi internal high-frequency (kontrak kuat, payload kompak, streaming); REST unggul untuk reach klien & observability gateway. Masing-masing dipakai di tempat yang tepat. `→ Canon: Newman (Building Microservices), komunikasi sinkron vs asinkron & kontrak service; Richards & Ford (Fundamentals), trade-off protokol`.
- **Trade-off:** Dua gaya API yang harus dirawat di `.Api`; tooling gRPC menambah build (.proto, codegen).
- **Kapan ditinjau ulang:** Bila gateway perlu satu protokol seragam ke klien non-web, atau gRPC-Web jadi kebutuhan UI langsung.

## Options considered

| Opsi | Pro | Kontra | Ankor |
|---|---|---|---|
| **A. gRPC internal + REST untuk UI** *(dipilih)* | Protokol tepat-guna per kanal; gateway tanpa transcoding | Dua gaya API; tooling gRPC | Newman (Building Microservices) |
| B. REST di semua jalur (termasuk internal) | Seragam, paling familiar | Kurang efisien & kontrak lebih longgar untuk internal high-frequency | Newman (Building Microservices) |
| C. gRPC di semua jalur (gRPC-Web ke UI) | Satu kontrak end-to-end | Beban browser/gateway; transcoding; debugging UI lebih sulit | Richards & Ford (Fundamentals) |

## Consequences

**Positif**
- Komunikasi sinkron lintas-service (read-API MasterData/Auth) efisien & ber-kontrak ketat ([ADR-0011](0011-master-data-read-api-cache-aside.md)).
- Gateway managed = bonus cert touch (APIM AZ-204 / API Gateway·Apigee PCD) tanpa kerja transcoding ([ADR-0018](0018-compute-hosting-mixed-paas.md)).

**Trade-off / lebih sulit**
- `.proto` (`<Module>.Grpc`) harus ber-versi & dikelola terpisah dari event contract ([ADR-0009](0009-contracts-vs-grpc-separation.md)).

**Yang harus dijaga**
- gRPC **murni service-to-service**; UI tak memanggil gRPC langsung. Komunikasi yang bisa asinkron tetap pakai event ([ADR-0005](0005-event-driven-outbox.md)), bukan gRPC.

## Out of scope / deferred

- gRPC-Web / gRPC streaming untuk UI real-time belum di-scope.
- Kontrak REST publik (OpenAPI) untuk konsumen eksternal belum dirinci.
