<script>
  import { vmService } from '$lib/services/vm-service';
  import VMExtensionDialog from '$lib/components/authed/bookings/dialogs/vm-extension-dialog.svelte';
  import VMConfigureSpecsDialog from '$lib/components/authed/bookings/dialogs/vm-configure-specs-dialog.svelte';
  import VMAttachStorageDialog from '$lib/components/authed/bookings/dialogs/vm-attach-storage-dialog.svelte';
  import VMAttachIsoDialog from '$lib/components/authed/bookings/dialogs/vm-attach-iso-dialog.svelte';
  import { vmListStore, selectedBookingStore } from '$lib/utils/store';
  import AlertDialog from '$lib/components/authed/alert-dialog.svelte';
  import { Download, Trash2, RefreshCcw, CalendarPlus, Zap, ChevronDown, MonitorUp, MonitorCog, HardDrive, FileCog } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
  import { Button } from '$lib/components/ui/button/index.js';
  import { goto } from '$app/navigation';

  let vmExtensionDialogOpen = $state(false);
  let configureSpecsDialogOpen = $state(false);
  let attachStorageDialogOpen = $state(false);
  let attachIsoDialogOpen = $state(false);
  let open = $state(false);

  // State for managing alert dialog visibility and content
  let alertState = $state({
    open: false,
    title: '',
    description: ''
  });

  // Stores the Promise resolver for alert dialog confirmation
  let resolveAction;

  // Handles alert dialog close event and resolves the Promise
  function handleNotify(event) {
    alertState.open = false;
    resolveAction?.(event.confirmed);
    resolveAction = null;
  }

  // Shows an alert dialog and returns a Promise that resolves with user's choice
  function promptUser(title, description) {
    return new Promise((resolve) => {
      alertState = { open: true, title, description };
      resolveAction = resolve;
    });
  }

  // Syncs booking data with backend and updates local stores
  async function refreshBooking() {
    try {
      const [updatedBooking, creds] = await Promise.all([vmService.getVMBookingById($selectedBookingStore.id), vmService.getVmInfo($selectedBookingStore.uuid)]);

      selectedBookingStore.set({
        ...$selectedBookingStore,
        ...updatedBooking,
        ...creds
      });

      vmListStore.update((bookings) => bookings.map((booking) => (booking.id === updatedBooking.id ? updatedBooking : booking)));

      toast.success('Refreshed booking details');
    } catch (error) {
      toast.error(error.message);
      if (error.message === 'Booking not found') history.back();
    }
  }

  // Handles booking deletion with user confirmation
  async function deleteBooking() {
    const userConfirmed = await promptUser(
      'Confirm Booking Deletion',
      'You are about to delete this booking. This action will erase the associated server permanently. Please confirm to proceed with deletion.'
    );

    if (!userConfirmed) return;

    try {
      await vmService.deleteVMBooking($selectedBookingStore.id);
      vmListStore.set(await vmService.getVMBookings());
      history.back();
      toast.success(`Deleted booking #${$selectedBookingStore.id}`);
    } catch (error) {
      toast.error(error.message);
    }
  }

  // Generates and downloads booking details as text file
  function handleFileDownload() {
    const output = [
      `Booking ID: ${$selectedBookingStore.id}\n`,
      '\nServer Details:',
      `- Ip: ${$selectedBookingStore.ip}`,
      `- Username: ${$selectedBookingStore.username}`,
      `- Password: ${$selectedBookingStore.password}`,
      `- Template: ${$selectedBookingStore.type}`,
      `- UUID: ${$selectedBookingStore.uuid}\n`,
      '\nOwner:',
      `- Profile: ${window.location.origin}/user/${$selectedBookingStore.owner.id}`,
      `- Name: ${$selectedBookingStore.owner.name} ${$selectedBookingStore.owner.surname}\n`,
      '\nTeacher assigned to:',
      `- Profile: ${window.location.origin}/user/${$selectedBookingStore.assigned.id}`,
      `- Name: ${$selectedBookingStore.assigned.name} ${$selectedBookingStore.assigned.surname}\n`,
      `\nCreated At: ${new Date($selectedBookingStore.createdAt).toLocaleString(undefined, { dateStyle: 'full', timeStyle: 'long' })}`,
      `Expired At: ${new Date($selectedBookingStore.expiredAt).toLocaleString(undefined, { dateStyle: 'full', timeStyle: 'long' })}`
    ].join('\n');

    const blob = new Blob([output], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `vm_${$selectedBookingStore.id}.txt`;
    a.click();
    window.URL.revokeObjectURL(url);
  }
  function handleResetPower() {
    vmService.resetVmPower($selectedBookingStore.uuid);
  }
</script>

<AlertDialog open={alertState.open} alertTitle={alertState.title} alertDescription={alertState.description} onNotify={handleNotify} />
<VMExtensionDialog bind:vmExtensionDialogOpen></VMExtensionDialog>
<VMConfigureSpecsDialog bind:configureSpecsDialogOpen></VMConfigureSpecsDialog>
<VMAttachStorageDialog bind:attachStorageDialogOpen></VMAttachStorageDialog>
<VMAttachIsoDialog bind:attachIsoDialogOpen></VMAttachIsoDialog>

<DropdownMenu.Root bind:open>
  <DropdownMenu.Trigger>
    <Button variant="outline" size="sm" class="border-primary text-primary hover:text-primary">Actions <ChevronDown class="size-4 ml-1 transition duration-100 {open ? 'rotate-180' : ''}" /></Button>
  </DropdownMenu.Trigger>
  <DropdownMenu.Content>
    <DropdownMenu.Group>
      <DropdownMenu.Label>Booking actions</DropdownMenu.Label>
      <DropdownMenu.Separator />
      <DropdownMenu.Item
        onmousedown={() => {
          window.open(`/api/web-console/${$selectedBookingStore.uuid}`, '_blank', 'noopener,noreferrer');
        }}
      >
        <MonitorUp class="mr-2 size-4" />
        <span>Web console</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={() => (configureSpecsDialogOpen = true)}>
        <MonitorCog class="mr-2 size-4" />
        <span>Configure Specs</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={() => (attachStorageDialogOpen = true)}>
        <HardDrive class="mr-2 size-4" />
        <span>Attach Storage</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={() => (attachIsoDialogOpen = true)}>
        <FileCog class="mr-2 size-4" />
        <span>Manage ISO</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={handleResetPower}>
        <Zap class="mr-2 size-4" />
        <span>Reset power</span>
      </DropdownMenu.Item>
      <DropdownMenu.Separator />
      <DropdownMenu.Item
        onmousedown={() => {
          vmExtensionDialogOpen = true;
        }}
      >
        <CalendarPlus class="mr-2 size-4" />
        <span>Extend booking</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={handleFileDownload}>
        <Download class="mr-2 size-4" />
        <span>Download</span>
      </DropdownMenu.Item>
      <DropdownMenu.Item onmousedown={refreshBooking}>
        <RefreshCcw class="mr-2 size-4" />
        <span>Refresh</span>
      </DropdownMenu.Item>
      <DropdownMenu.Separator />
      <DropdownMenu.Item onmousedown={deleteBooking} class="hover:data-[highlighted]:bg-destructive hover:data-[highlighted]:text-white">
        <Trash2 class="mr-2 size-4" />
        <span>Delete</span>
      </DropdownMenu.Item>
    </DropdownMenu.Group>
  </DropdownMenu.Content>
</DropdownMenu.Root>
