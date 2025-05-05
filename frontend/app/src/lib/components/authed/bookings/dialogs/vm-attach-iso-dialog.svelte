<script>
  import { vmService } from '$lib/services/vm-service';
  import * as Dialog from '$lib/components/ui/dialog';
  import { Label } from '$lib/components/ui/label';
  import { Button } from '$lib/components/ui/button/index.js';
  import * as Select from '$lib/components/ui/select/index.js';
  import { LoaderCircle } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { selectedBookingStore } from '$lib/utils/store';

  let { attachIsoDialogOpen = $bindable() } = $props();

  let loadingStates = {
    attachIso: false,
    detachIso: false
  };

  let systemConfig = $state({
    vmName: null,
    isoName: null
  });

  $effect(() => {
    systemConfig.vmName = $selectedBookingStore.uuid;
  });

  let isoOptions = $derived($selectedBookingStore.isoList);

  async function handleAttachIso() {
    if (!systemConfig.isoName) {
      toast.error('Please select an ISO file');
      return;
    }

    loadingStates.attachIso = true;

    try {
      await vmService.attachIso(systemConfig);
      toast.success(`ISO ${systemConfig.isoName} attached successfully`);
      attachIsoDialogOpen = false;
    } catch (error) {
      toast.error(error.message);
    } finally {
      loadingStates.attachIso = false;
    }
  }

  async function handleDetachIso() {
    loadingStates.detachIso = true;

    try {
      await vmService.detachIso(systemConfig.vmName);
      toast.success('ISO detached successfully');
      attachIsoDialogOpen = false;
    } catch (error) {
      toast.error(error.message);
    } finally {
      loadingStates.detachIso = false;
    }
  }
</script>

<Dialog.Root bind:open={attachIsoDialogOpen}>
  <Dialog.Content class="sm:max-w-[425px]">
    <Dialog.Header>
      <Dialog.Title>ISO Management</Dialog.Title>
      <Dialog.Description>Attach or detach ISO files to your virtual machine</Dialog.Description>
    </Dialog.Header>

    <div class="grid gap-4 py-4">
      <div class="grid grid-cols-4 items-center">
        <Label>ISO File</Label>
        <div class="col-span-3">
          <Select.Root bind:value={systemConfig.isoName} name="Select ISO" type="single">
            <Select.Trigger class="w-full truncate">
              {systemConfig.isoName ? systemConfig.isoName : 'Select ISO file'}
            </Select.Trigger>
            <Select.Content>
              <Select.Group>
                <Select.GroupHeading>Available ISO Files</Select.GroupHeading>
                {#each isoOptions as iso}
                  <Select.Item value={iso}>
                    {iso}
                  </Select.Item>
                {/each}
              </Select.Group>
            </Select.Content>
          </Select.Root>
        </div>
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onmousedown={() => (attachIsoDialogOpen = false)}>Cancel</Button>
      <Button type="submit" disabled={loadingStates.detachIso} onclick={handleDetachIso}>
        {#if loadingStates.detachIso}
          <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Detach ISO
      </Button>
      <Button type="submit" disabled={loadingStates.attachIso} onclick={handleAttachIso}>
        {#if loadingStates.attachIso}
          <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
        {/if}
        Attach ISO
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
