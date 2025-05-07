<script>
  import { vmService } from '$lib/services/vm-service';
  import * as Dialog from '$lib/components/ui/dialog';
  import { Label } from '$lib/components/ui/label';
  import { Button } from '$lib/components/ui/button/index.js';
  import * as Select from '$lib/components/ui/select/index.js';
  import * as Alert from '$lib/components/ui/alert/index.js';
  import { LoaderCircle } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { selectedBookingStore } from '$lib/utils/store';

  let { attachStorageDialogOpen = $bindable() } = $props();

  $effect(() => {
    systemConfig.vmName = $selectedBookingStore.uuid;
  });

  let loadingStates = {
    attachStorage: false
  };

  let systemConfig = $state({
    vmName: null,
    selectedStorage: null
  });

  const storageOptions = [50, 100, 200, 500];

  let constraints = $derived({
    storage: { validSizes: storageOptions }
  });

  async function handleUpdateConfig() {
    if (!constraints.storage.validSizes.includes(String(systemConfig.selectedStorage))) {
      toast.error(`Storage must be one of: ${constraints.storage.validSizes.map((s) => `${s}GB`).join(', ')}`);
    }

    loadingStates.attachStorage = true;

    try {
      await vmService.attachStorage(systemConfig);
      toast.success(`${systemConfig.selectedStorage}GB storage attached. Effects will take place in a minute`);
      attachStorageDialogOpen = false;
    } catch (error) {
      toast.error(error.message);
    } finally {
      loadingStates.attachStorage = false;
    }
  }
</script>

<Dialog.Root bind:open={attachStorageDialogOpen} }>
  <Dialog.Content class="sm:max-w-[425px]">
    <Dialog.Header>
      <Dialog.Title>System Configuration</Dialog.Title>
      <Dialog.Description>Attach storage to your virtual machine</Dialog.Description>
    </Dialog.Header>
    <Alert.Root>
      <Alert.Title>Heads up!</Alert.Title>
      <Alert.Description class="text-xs">Storage cannot be detached from your VM after attachment. Please attach storage carefully.</Alert.Description>
    </Alert.Root>

    <div class="grid gap-4 py-4">
      <div class="grid grid-cols-5 items-center">
        <Label>Storage</Label>
        <div class="col-span-4">
          <Select.Root bind:value={systemConfig.selectedStorage} name="Select Storage" type="single">
            <Select.Trigger class="w-full">
              {systemConfig.selectedStorage ? `${systemConfig.selectedStorage} GB` : 'Select storage'}
            </Select.Trigger>
            <Select.Content>
              <Select.Group>
                <Select.GroupHeading>Storage Options</Select.GroupHeading>
                {#each constraints.storage.validSizes as size}
                  <Select.Item value={size}>
                    {size} GB
                  </Select.Item>
                {/each}
              </Select.Group>
            </Select.Content>
          </Select.Root>
        </div>
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onmousedown={() => (attachStorageDialogOpen = false)}>Cancel</Button>
      <Button type="submit" disabled={loadingStates.attachStorage} onclick={handleUpdateConfig}>
        {#if loadingStates.attachStorage}
          <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Attach Storage
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
