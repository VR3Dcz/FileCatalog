# FileCatalog

FileCatalog is a high-performance, cross-platform file cataloging application built with C#, .NET 8, and Avalonia UI. It is designed to effortlessly scan, index, and search millions of files and folders across massive enterprise-grade drives without compromising memory or UI responsiveness.

<img width="1201" height="746" alt="FileCatalog" src="https://github.com/user-attachments/assets/c3c2c3e5-2385-4131-a0fc-1d3247b5ed19" />

# User Manual

This guide will help you navigate the application, scan your drives, and utilize the lightning-fast search engine to find your files instantly.

---
## 0. Download and run
[![Download Latest Release](https://img.shields.io/github/v/release/VR3Dcz/FileCatalog?label=Download%20Latest%20Version&style=for-the-badge&color=success&logo=github)](https://github.com/VR3Dcz/FileCatalog/releases/latest)

No installaton necessary, just download, unzip and run.

## 1. Scanning Your Drives

To start cataloging your files, you need to scan a drive or a specific folder.

* Click the **Scan** button in the top toolbar.
* Select the folder or entire drive (e.g., `C:\` or `F:\`) you want to index.
* Wait for the progress bar to complete. The app will extract file names, sizes, modification dates, and even audio metadata (ID3 tags) if enabled.

> Note: Scanning a massive drive for the first time might take a few minutes, but once indexed, browsing and searching are instantaneous.

---

## 2. Navigating the Catalog

The main window is divided into a folder tree on the left and a detailed file grid on the right.

* **Browse:** Click any folder in the left panel to view its contents in the right grid. Double-click a folder in the right grid to enter it.
* **Calculate Folder Size:** Right-click a folder in the main grid and select **Calculate Folder Size** to see exactly how much space it takes up (including all subfolders).
* **Manage Drives:** Right-click a root drive in the left panel to rename (F2), rescan, remove, or change its display order (Move Up/Down).
* **Customize Columns:** Go to **View > Columns** in the top menu to toggle the visibility of Path, Extension, Size, and Date columns.

---

## 3. Searching for Files

FileCatalog features a highly optimized Full-Text Search (FTS5) engine that delivers results in milliseconds.

* Type your query into the search box in the top toolbar and press **Enter** (or click the Search button).
* Check the **Regex** box to use advanced Regular Expressions for your search.

### Search Examples

| Search Type | Input Example | Description |
| :--- | :--- | :--- |
| Standard | `project` | Finds files and folders containing the word "project". |
| Standard | `*.pdf` | Finds all PDF files. |
| Regex | `^doc_\d{4}` | Finds files starting with "doc_" followed by 4 digits. |
| Regex | `\.(mp3\|flac)$` | Finds all audio files ending in exactly .mp3 or .flac. |

---

## 4. Saving and Opening Catalogs

Your scanned data is kept in a temporary high-speed workspace until you manually save it.

* **Save:** Go to **File > Save As** to store your current catalog as a highly compressed `.kat` file.
* **Open:** Go to **File > Open** to load a previously saved `.kat` file.
* **Auto-Open:** Go to **Settings > Preferences** and check the "Auto-open last catalog" option. The app will automatically load your most recent database upon launch.

---

## 5. Advanced Tips

* **Message History:** Double-click the status bar at the very bottom of the window to open a chronological log of all background tasks, errors, and scan times.
* **Quick Rename:** Select a folder in the left tree and press **F2** to quickly customize how it appears in your catalog without affecting the real folder on your hard drive.

Note for Windows Users:
Because this is a free, unsigned open-source application, Windows SmartScreen may flag it upon first launch. To run the application, click "More info" and then "Run anyway".

## 🚀 Key Architectural Features

This project was built with strict adherence to performance and architectural best practices, specifically optimized for massive I/O throughput and low memory allocation.

* **Extreme I/O Throughput (Zero-Allocation Loop):** Bypasses heavy ORMs during mass inserts. Uses raw, parameterized ADO.NET (`SqliteCommand.Prepare()`) combined with strictly scoped database transactions (Unit of Work) and `PRAGMA synchronous = OFF` to achieve maximum write speeds to the SQLite database without thrashing the disk head.
* **Lightning-Fast Full-Text Search (FTS5):** Searching through millions of files takes milliseconds. The application leverages SQLite's native **FTS5 inverted indexes**, updated automatically via database triggers, completely offloading the search burden from the C# application memory.
* **Native C# Regex Integration:** Maps the .NET Regular Expression engine directly into the SQLite memory space as a User-Defined Function (UDF), allowing complex pattern matching directly within SQL queries.
* **Absolute UI Virtualization:** Replaces standard data grids with Avalonia's highly optimized `TreeDataGrid` and `AvaloniaList`. It utilizes bidirectional UI virtualization to render only the visible pixels, ensuring a smooth, locked at 60 FPS scrolling experience even when displaying directories with hundreds of thousands of files.
* **Safe Async Workflows & Concurrency Limiters:** Eliminates the `async void` anti-pattern using custom `SafeFireAndForget` extensions. Parallel I/O tasks (like reading ID3 tags via TagLib#) are strictly bound to `Environment.ProcessorCount` to prevent ThreadPool starvation and disk thrashing.



## 🛠️ Tech Stack

* **Framework:** .NET 8.0
* **UI Framework:** Avalonia UI (Cross-platform: Windows, macOS, Linux)
* **Database:** SQLite (with FTS5 extension)
* **Data Access:** Raw ADO.NET (for high-performance Bulk Inserts) + Dapper (for lightweight read queries)
* **Architecture:** MVVM (Model-View-ViewModel) using CommunityToolkit.Mvvm
* **Audio Metadata:** TagLib#


