# Interview Notes

A concise way to explain the design:

> I did not start by adding a pool or a scheduler. The original problem was that generic item templates created branches that were never used, and lazy creation alone could move the spike to first access. The new runtime separates four questions: whether structure needs to exist, whether state changed, whether the current frame can afford the work, and whether a delayed result still belongs to the current binding. Lazy fragments answer the first, dirty-state commits answer the second, the shared scheduler answers the third, and scope/binding tokens plus bounded pooling answer the fourth.

Important trade-offs to mention:

- A budget scheduler does not make a large Instantiate interruptible.
- Pooling reduces rebuild cost but retains memory.
- Dirty coalescing is correct for state, not automatically for one-shot events.
- Virtualization improves long lists by limiting active views, but fixed-size and dynamic-size lists need different algorithms.
- Performance claims require target-device A/B data; architecture alone is not evidence.
