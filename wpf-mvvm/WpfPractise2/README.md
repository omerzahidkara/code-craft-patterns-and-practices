WPF Prime Explorer — Notes & Lessons

This repository is a WPF/MVVM exercise that finds primes up to high limits, offers a two-column nearest prime search window, and optionally manages multi-window behavior with a Focus Mode. The project brings together the Segmented Sieve algorithm, Task/async concurrency, clean MVVM binding/commands, and a light window manager.

Goal: show how the right algorithm, the right UI-thread model, and a tidy MVVM structure make a desktop app feel fast and clear.

Table of Contents

Highlights

Architecture Overview

Algorithms

Segmented Sieve

Concurrency Model

Task / async-await: why & how

UI & MVVM

Commands & RaiseCanExecuteChanged

Search Window (Two-Column Layout)

Focus Mode

Sequence Diagrams (paste into sequencediagramorg)

Running the App

Top Menu Enhancements & Next Steps

License

Highlights

Segmented Sieve: fast prime scanning at 10M–20M+ with O(n log log n) behavior and bounded memory.

Task/async: long work without freezing the UI; progress reporting and cancellation.

MVVM: ViewModels own logic; Views stay slim via bindings and commands.

Search Window: given a number, shows the nearest prime and lists greater-than and less-than primes in two readable columns.

Focus Mode: optional—when enabled, the active window minimizes others; on close, the previously active window comes to front. Default is off.
