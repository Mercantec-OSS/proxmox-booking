<script>
  import * as Card from '$lib/components/ui/card';
  import * as Table from '$lib/components/ui/table';
  import { vmService } from '$lib/services/vm-service';
  import { Skeleton } from '$lib/components/ui/skeleton/index.js';

  let { clusterInfo } = $props();

  // Fetches latest resource utilization data
  async function fetchClusterInfo() {
    clusterInfo = await vmService.getClusterInfo();
  }

  // Set up polling with interval cleanup on leave
  $effect(() => {
    const updateIntervalId = setInterval(fetchClusterInfo, 60000);
    return () => clearInterval(updateIntervalId);
  });
</script>

<Card.Root>
  <Card.Header>
    <h2 class="text-lg font-semibold md:text-xl">Cluster info</h2>
  </Card.Header>
  <Card.Content>
    <Table.Root>
      <Table.Header>
        <Table.Row>
          <Table.Head class="table-cell">Total CPU</Table.Head>
          <Table.Head class="table-cell">Used CPU</Table.Head>
          <Table.Head class="table-cell">Total Ram</Table.Head>
          <Table.Head class="table-cell">Used Ram</Table.Head>
          <Table.Head class="table-cell">Total storage</Table.Head>
          <Table.Head class="table-cell">Used storage</Table.Head>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        <Table.Row>
          {#each ['cpuTotal', 'cpuUsage', 'ramTotal', 'ramUsage', 'storageTotal', 'storageUsage'] as resource}
            <Table.Cell>
              {#if clusterInfo?.[resource]}
                {clusterInfo[resource]}
              {:else}
                <Skeleton class="h-6 w-20 rounded-lg" />
              {/if}
            </Table.Cell>
          {/each}
        </Table.Row>
      </Table.Body>
    </Table.Root>
  </Card.Content>
</Card.Root>
