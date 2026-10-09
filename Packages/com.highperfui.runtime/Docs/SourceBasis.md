# Source Basis and Scope

This Unity package is a new implementation based on the actual problem pattern reviewed in the supplied commercial source and the later design discussion.

Observed original characteristics included:

- generic item templates with optional branches;
- selected child subtrees kept as lazy templates;
- first access synchronously cloned a selected subtree;
- special visibility helpers avoided instantiating a branch merely to hide it;
- queued calls were replayed in order rather than semantically diffed/coalesced;
- same-structure fast cloning had structural constraints;
- some business call sites used coroutines to spread item creation.

The new Unity runtime intentionally adds capabilities that were not established as part of that original core: shared frame-budget scheduling, dirty-state coalescing, bounded pools, binding-generation guards, fixed-list virtualization and diagnostics.
