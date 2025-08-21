WPF Prime Explorer --- Notes & Lessons
====================================

This repository is a WPF/MVVM exercise that finds primes up to high limits, offers a two-column **nearest prime search** window, and optionally manages multi-window behavior with a **Focus Mode**. The project brings together the **Segmented Sieve** algorithm, **Task/async** concurrency, clean **MVVM** binding/commands, and a light **window manager**.

> Goal: show how the right algorithm, the right UI-thread model, and a tidy MVVM structure make a desktop app feel fast and clear.

Table of Contents
-----------------

-   [Highlights](#highlights)

-   [Architecture Overview](#architecture-overview)

-   [Algorithms](#algorithms)

    -   [Segmented Sieve](#segmented-sieve)

-   [Concurrency Model](#concurrency-model)

    -   [`Task` / `async-await`: why & how](#task--asyncawait-why--how)

-   [UI & MVVM](#ui--mvvm)

    -   [Commands & `RaiseCanExecuteChanged`](#commands--raisecanexecutechanged)

    -   [Search Window (Two-Column Layout)](#search-window-two-column-layout)

    -   [Focus Mode](#focus-mode)

-   [Sequence Diagrams (paste into sequencediagramorg)](#sequence-diagrams-paste-into-sequencediagramorg)

-   [Running the App](#running-the-app)

-   [Top Menu Enhancements & Next Steps](#top-menu-enhancements--next-steps)

-   [License](#license)


Highlights
----------

-   **Segmented Sieve**: fast prime scanning at 10M--20M+ with **O(n log log n)** behavior and bounded memory.

-   **Task/async**: long work without freezing the UI; progress reporting and cancellation.

-   **MVVM**: ViewModels own logic; Views stay slim via bindings and commands.

-   **Search Window**: given a number, shows the **nearest prime** and lists greater-than and less-than primes in two readable columns.

-   **Focus Mode**: optional---when enabled, the active window minimizes others; on close, the previously active window comes to front. Default is **off**.


WPF UI (Views)

   ├─ MainWindow          (menu, parameters, 150px progress, chunk summaries)

   └─ PrimeSearchWindow   (nearest prime search; two-column result lists)

ViewModels

   ├─ MainViewModel          (scan, cancel, progress, segment summaries)

   └─ PrimeSearchViewModel   (nearest-prime search, two lists, messages)

Core / Infra

   ├─ SegmentedSieve (algorithm; segmented Eratosthenes)

   ├─ RelayCommand / AsyncRelayCommand (commands)

   └─ WindowManager (optional focus behavior; default disabled)


Algorithms
----------

### Segmented Sieve

**Why**\
The naïve "trial division up to √n for every n" is **O(n√n)**. It degrades badly as limits grow. The **Segmented Sieve** keeps Eratosthenes' cache-friendly pattern and uses bounded memory.

**How**

1.  Compute base primes up to `√N` once.

2.  Slide over `[2..N]` in segments `[low..high]`.

3.  In each segment, mark composites using only the base primes.

4.  Emit segment summaries (count, last prime), update progress, continue.

**Advantages**

-   Predictable memory footprint (segment-sized).

-   Excellent performance in practice (**O(n log log n)** overall).


Concurrency Model
-----------------

### `Task` / `async-await`: why & how

**Why**\
WPF has a single UI thread. Long work must not block it. Users should see progress and be able to cancel.

**How**

-   Offload **CPU-bound** work via `Task.Run` (ThreadPool).

-   Use `await` for non-blocking waits (e.g., `Task.Delay`) so you don't hold threads.

-   Report progress with `IProgress<T>`.

-   Support cancellation with `CancellationToken`.

-   Marshal UI updates back to the dispatcher when needed.


UI & MVVM
---------

### Commands & `RaiseCanExecuteChanged`

Button enablement is computed by command `CanExecute`. Whenever a property changes that affects this logic, call `RaiseCanExecuteChanged()` so WPF re-queries and the UI updates immediately (e.g., start disabled while a scan runs; cancel enabled only while a scan runs).

### Search Window (Two-Column Layout)

Given an input `n`, the app finds the **nearest prime** (exact match, or the closer of below/above). Results render in two vertical lists: **greater-than primes** on the left and **less-than primes** on the right. The nearest prime is announced as text above the lists (not embedded into them). If the app has collected primes (`CollectPrimes=true`), the search walks those; otherwise it runs a local segmented sieve around `n`.

### Focus Mode

A toggle in the top menu controls window behavior:

-   **Enabled**: activating a window minimizes other visible app windows; closing a window brings the previously active one to front.

-   **Disabled (default)**: standard OS windowing---no automatic minimize/restore.

Bindings keep the menu check state and the actual behavior in sync.


Top Menu Enhancements & Next Steps
----------------------------------

-   **Top Menu Enhancements**

    -   **Export**: save results (e.g., chunk summaries or search output) to `.txt`/`.csv`.

    -   **Mathematical Formulae**: built-in computations (e.g., prime counts, gaps statistics in the current range, quick estimates) accessible from the menu.

    -   **Configurable Main Action**: allow the main "Compute Primes" button to run one of the menu's calculations instead of a full scan.

    -   **Rules-Based Social Messages**: show contextual notifications/toasts (e.g., "Large limit detected---consider a bigger segment size", "Computation paused; try narrowing the range") driven by simple logical rules.

-   **Polish**

    -   Persist user preferences (Focus Mode, window size/position, segment size).

    -   Keyboard shortcuts (e.g., `Ctrl+F` for search, `Ctrl+Shift+O` for focus toggle).

    -   Lightweight telemetry: per-segment elapsed time, overall ETA.


It's been rewarding to see how much the Segmented Sieve boosts performance, how `Task/async` keeps the UI responsive, and how a modest, optional Focus Mode can simplify multi-window work. 

![Image](https://github.com/user-attachments/assets/ac9e8f25-586f-4a1d-912b-e2dad78738ba)

