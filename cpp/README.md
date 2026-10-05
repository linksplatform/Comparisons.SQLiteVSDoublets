<!-- markdownlint-disable MD043 -->
# C++ placeholder

This directory contains an empty Conan manifest and no C++ implementation,
`CMakeLists.txt`, tests, or package metadata. The obsolete build and release
workflow was retired in issue #110; it could not build or publish this tree.

A future C++ implementation must add a real build target and tests before
enabling CI. Use a supported Clang version on a pinned runner, Conan 2 profiles
and generators, pull request validation, and release jobs gated on passing tests.
