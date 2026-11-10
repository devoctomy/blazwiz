global using Reqnroll;
global using Xunit;

// The sign out everywhere scenario revokes every session the test user has, so scenarios run one at a time; each signs
// in afresh.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
