# FileCatalog

FileCatalog is a high-performance, cross-platform file cataloging application built with C#, .NET 8, and Avalonia UI. It is designed to effortlessly scan, index, and search millions of files and folders across massive enterprise-grade drives without compromising memory or UI responsiveness.



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

## ⚙️ Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

### Build and Run
1. Clone the repository:
   ```bash
   git clone [https://github.com/YOUR_USERNAME/FileCatalog.git](https://github.com/YOUR_USERNAME/FileCatalog.git)

```

2. Navigate to the project directory:
```bash
cd FileCatalog

```


3. Restore dependencies and run the application:
```bash
dotnet run --project FileCatalog

```



## 📂 How It Works

1. **Scan:** Click "Scan New Drive" and select a directory. The application utilizes iterative Breadth-First Search (BFS) to prevent StackOverflow exceptions on deep directory trees.
2. **Process:** Audio metadata is parsed asynchronously on background threads. Data is funneled into a massive `CatalogBulkInserter` pipe.
3. **Search:** Use standard text or check the "Regex" box for advanced queries. Results are served instantly via the underlying FTS5 engine.

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the issues page. If you want to contribute to the codebase, please ensure your PRs maintain the strictly typed, allocation-free architectural guidelines established in the core services.
