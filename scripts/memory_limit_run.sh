#!/bin/sh
# Runs a benchmark inside the memory-limited systemd scope it is started in, for at most SECONDS.
#
# Usage: memory_limit_run.sh STATS SECONDS COMMAND...
#
# Every 15 minutes it prints a sample of the scope: the benchmark's state and kernel wait channel, its
# file pages, the dirty ones and those under writeback, its major page faults and the memory and I/O
# pressure. At the end it writes memory.peak, memory.stat and memory.events to STATS; this shell outlives
# an OOM kill or the timeout of the benchmark, so the stats show them. It exits with the benchmark's
# status, 124 if the time ran out (see timeout(1)).
stats=$1 seconds=$2
shift 2
cgroup=/sys/fs/cgroup$(cut -d: -f3- /proc/self/cgroup)
(
  while sleep 900; do
    echo "== scope sample at $(date -u +%H:%M:%S)"
    ps -o etime=,stat=,wchan:32=,comm= -p "$(paste -sd, "$cgroup/cgroup.procs")"
    grep -E '^(file|file_dirty|file_writeback|pgmajfault|workingset_refault_file) ' "$cgroup/memory.stat"
    for pressure in memory io; do
      [ -r "$cgroup/$pressure.pressure" ] && sed "s/^/$pressure /" "$cgroup/$pressure.pressure"
    done
  done
) &
sampler=$!
timeout "$seconds" "$@"
status=$?
pkill -P "$sampler"
kill "$sampler" 2>/dev/null
wait "$sampler"
{ cat "$cgroup/memory.peak"; cat "$cgroup/memory.stat"; cat "$cgroup/memory.events"; } > "$stats"
exit "$status"
