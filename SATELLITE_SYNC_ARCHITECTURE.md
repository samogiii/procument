# Satellite Architecture & Data Sync Specification

This document outlines the requirements and architectural design for cloning the current "Procument" application into a lightweight "Satellite" version and implementing a secure, encrypted data synchronization system.

## 1. Objective
Create a specialized, lightweight version of the application (the **Satellite App**) that manages only **Base 2** Customers and RFQs, and synchronizes all changes with the **Main App (Hub)** using industry-standard encryption and digital signatures.

---

## 2. High-Level Architecture

### The Satellite App (Source)
*   **Scope:** Restricted subset of the database (Users, Customers, RFQs, RFQItems, PartNumbers).
*   **Restriction:** Logic is hard-coded or filtered at the DbContext level to only permit data where `Customer.Base == 2`.
*   **Role:** Acts as a specialized entry point for specific users/customers.

### The Main App (Hub)
*   **Scope:** Full application containing all modules and all data (Base 1, 2, 3, etc.).
*   **Role:** Acts as the "Central Source of Truth" and the receiver for all satellite data.

---

## 3. Data Mapping & Sync Logic

Since the two applications maintain separate databases, **Primary Keys (IDs) will not match**.

### The SyncRegistry Table (Main App)
A new registry must be implemented in the Hub to map IDs across the distributed system.

| Column | Type | Description |
| :--- | :--- | :--- |
| `EntityName` | String | "RFQ", "Customer", "User", "PartNumber" |
| `MainAppId` | Long | The ID in the Hub database |
| `SatelliteAppId` | Long | The ID in the Satellite database |
| `SatelliteName` | String | Identifier for which satellite sent the data |
| `SyncStatus` | Enum | Synced, Pending, Error |
| `LastSyncAt` | DateTime | Timestamp of the last successful update |
| `DataHash` | String | SHA-256 hash of the content to prevent redundant updates |

### Upsert Strategy (Update or Insert)
1.  **Incoming Data:** Hub receives a packet from Satellite with `SatelliteAppId`.
2.  **Lookup:** Hub checks `SyncRegistry` for a match.
3.  **Match Found:** Hub updates the existing record linked to that `MainAppId`.
4.  **No Match:** Hub checks for **Natural Keys** (e.g., Email for Users, Name for Customers).
5.  **Final Step:** If still no match, Hub creates a new record and adds the mapping to `SyncRegistry`.

---

## 4. Security & Encryption Requirements

To ensure data integrity and prevent impersonation, the system uses a **Hybrid RSA/AES** approach.

### Public/Private Key Infrastructure (RSA)
*   **Digital Signatures:** The Satellite App signs every request with its **Private Key**. The Hub verifies the signature using the Satellite's **Public Key**. This proves the data originated from the trusted Satellite App.
*   **Identity Proof:** Requests without a valid signature are rejected immediately.

### Payload Encryption (AES-256)
*   **Confidentiality:** The actual JSON data (containing prices, customer names, etc.) is encrypted using a shared **AES-256 Secret Key**.
*   **Transport:** Even if the network traffic is intercepted, the data remains unreadable without the secret key.

### Security Key Management Table
A table in the Hub to manage satellite credentials:
*   `AppIdentifier`: "Satellite_Base2"
*   `PublicKey`: RSA Public Key (Base64)
*   `IsRevoked`: Boolean flag to kill the sync if security is compromised.

---

## 5. Functional Requirements

### Lightweight App (Satellite)
1.  **Query Filtering:** Implement Global Query Filters in EF Core:
    `modelBuilder.Entity<Customer>().HasQueryFilter(c => c.Base == 2);`
2.  **Outbound Worker:** A background service (using Hangfire or a Hosted Service) that monitors the "Outbox" and pushes changes to the Hub.
3.  **Authentication:** Shared Secret + Digital Signature in the API Header.

### Main App (Hub)
1.  **Sync API:** Secure endpoints (e.g., `/api/sync/customers`, `/api/sync/rfqs`).
2.  **Validation:** Ensure that incoming data from the Satellite *actually* has `Base == 2` before saving.
3.  **Audit Logging:** Detailed logs of every sync attempt, including encryption success/failure and ID mapping results.

---

## 6. Implementation Roadmap

1.  **Phase 1:** Define and migrate the `SyncRegistry` and `SyncSecurity` tables in the Main App.
2.  **Phase 2:** Implement the Encryption/Decryption and Signature Verification services.
3.  **Phase 3:** Create the Hub Sync API Endpoints.
4.  **Phase 4:** Clone the Procument repo, strip unrelated modules, and apply "Base 2" filters.
5.  **Phase 5:** Implement the Outbound Pushing logic in the Satellite App.
6.  **Phase 6:** Initial Data Migration (Seed the Satellite with existing Base 2 data from the Hub).
