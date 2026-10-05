## Reproduction (released 0.5.0 and current main)

The `new_cap > current` branch in `doublets/src/mem/mod.rs::resize_mem`
returns `RawMem::grow_filled(...)`. `platform-mem` 0.3.0 returns only the added
region, whereas the unit store treats this slice as its complete allocation.
The initial growth from 8,192 to 2^20 records therefore exposes only 1,040,384
records while the header reserves 2^20 - 1.

A complete executable reproduction already exists in
[Comparisons.SQLiteVSDoublets/experiments/unit_store_growth](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/tree/main/experiments/unit_store_growth).

```sh
git clone https://github.com/linksplatform/Comparisons.SQLiteVSDoublets.git
cd Comparisons.SQLiteVSDoublets
cargo build --manifest-path experiments/unit_store_growth/Cargo.toml --locked --release
(ulimit -v 524288; RUST_BACKTRACE=full experiments/unit_store_growth/target/release/unit_store_growth bare)
(ulimit -v 524288; RUST_BACKTRACE=full experiments/unit_store_growth/target/release/unit_store_growth whole)
```

On Linux x86_64 / Rust 1.98.1, the finite probe has a 512 MiB virtual-memory
limit. `bare` panics creating link 1,040,384 with `Data part should be in data
memory` and returns `Err(Any { .. })`. The program intentionally catches the
panic, so its process exit code is zero; inspect its output. `whole` completes
2,097,153 links, including subsequent growth, and prints `Ok(Ok(2097153))`.
The unmodified call remains present on current main, so this is not solely
an obsolete-release problem. The earlier benchmark failure is recorded in
[run 37224809082](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37224809082).

## Workaround

Wrap the RawMem backend so grow/grow_filled return the whole allocation.
The comparison repository already does this in
[rust/src/memory.rs](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/blob/main/rust/src/memory.rs)
(`Whole<M>`), for both volatile and file-mapped backends.

## Suggested code fix

In `resize_mem`, perform `m.grow_filled(new_cap - current, M::Item::default())?;`
then return `Ok(m.allocated_mut())`, matching the shrink/no-change branches.
Check other growth call sites for the same slice-contract assumption. Add
tests asserting initial capacity and subsequent growth retain earlier links
for unit/split and global/file-mapped stores. Small mock RawMem backends can
exercise the contract without creating millions of links in unit tests.
