<script>
  import { vmService } from '$lib/services/vm-service';
  import { selectedBookingStore } from '$lib/utils/store';
  import VMActionsDropdown from '$lib/components/authed/bookings/vm-actions-dropdown.svelte';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';
  import { afterNavigate } from '$app/navigation';
  import { Button } from '$lib/components/ui/button';
  import * as Card from '$lib/components/ui/card';
  import { Badge } from '$lib/components/ui/badge';
  import { Label } from '$lib/components/ui/label';
  import * as Avatar from '$lib/components/ui/avatar/index.js';
  import { Textarea } from '$lib/components/ui/textarea';
  import { Skeleton } from '$lib/components/ui/skeleton';
  import { ChevronLeft, ArrowRight, Trash, Check, LoaderCircle } from 'lucide-svelte';
  import CPUUsageChart from '$lib/components/authed/bookings/cpu-usage-chart.svelte';
  import RamUsageChart from '$lib/components/authed/bookings/ram-usage-chart.svelte';
  import DiskWriteChart from '$lib/components/authed/bookings/disk-write-chart.svelte';

  // Props and data initialization
  const { data } = $props();
  const { vmData, userInfo, errorMessage } = data;

  // State management
  let loadingDelete = $state(false);
  let loadingAccept = $state(false);

  // Initialize selected booking store
  $effect(() => {
    selectedBookingStore.set(vmData);
  });

  // Constants
  const metrics = ['CPU', 'Memory', 'Disk'];
  const dateFormatter = new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  });

  // Derived values
  let userAuthed = $derived(userInfo.role === 'Admin' || userInfo.role === 'Teacher');
  let users = $derived([
    { label: 'Owner', user: $selectedBookingStore?.owner },
    { label: 'Teacher', user: $selectedBookingStore?.assigned }
  ]);

  const formatDateTime = (date) => dateFormatter.format(new Date(date)).replace(',', '');

  async function fetchVmCreds() {
    try {
      const { ip, username, password, isPowerOn } = await vmService.getVmInfo($selectedBookingStore.uuid);
      selectedBookingStore.update((store) => ({ ...store, ip, username, password, isPowerOn }));
    } catch (error) {
      toast.error(error.message);
    }
  }

  async function handleAcceptBooking() {
    if (loadingAccept) return;

    try {
      loadingAccept = true;
      await vmService.acceptVMBooking($selectedBookingStore.id);
      $selectedBookingStore.isAccepted = true;
      toast.success('Accepted booking');
    } catch (error) {
      toast.error(error.message);
    } finally {
      loadingAccept = false;
    }
  }

  async function handleDeleteBooking() {
    if (loadingDelete) return;

    try {
      loadingDelete = true;
      await vmService.deleteVMBooking($selectedBookingStore.id);
      toast.success('Booking deleted');
      history.back();
    } catch (error) {
      toast.error(error.message);
    } finally {
      loadingDelete = false;
    }
  }

  function copyToClipboard(text) {
    navigator.clipboard.writeText(text);
    toast.success('Copied to clipboard');
  }

  function checkErrors() {
    if (errorMessage) {
      toast.error(errorMessage);
      selectedBookingStore.set(null);

      setTimeout(() => history.back(), 1500);
      return true;
    }
    return false;
  }

  afterNavigate(() => {
    const errors = checkErrors();
    if (errors) {
      return;
    }

    if (!$selectedBookingStore.ip && $selectedBookingStore.isAccepted) {
      fetchVmCreds();
    }
  });
</script>

<main class="grid flex-1 items-start gap-4 p-4 sm:px-6 md:gap-8">
  {#if $selectedBookingStore && !data.errorMessage}
    <div class="mx-auto grid flex-1 auto-rows-max gap-4">
      <div class="flex flex-wrap items-center gap-4">
        <Button onmousedown={() => history.back()} variant="outline" size="icon" class="size-7">
          <ChevronLeft class="size-4" />
          <span class="sr-only">Back</span>
        </Button>
        <h1 class="flex-1 shrink-0 whitespace-nowrap text-xl font-semibold tracking-tight sm:grow-0">Booking details</h1>
        <div class="inline-flex items-center gap-x-2">
          <div class="size-2 rounded-full {$selectedBookingStore.isPowerOn ? 'bg-green-500' : 'bg-red-500'}"></div>
          <p class="text-sm {$selectedBookingStore.isPowerOn ? 'text-green-500' : 'text-red-500'}">{$selectedBookingStore.isPowerOn ? 'Online' : 'Offline'}</p>
        </div>
        <div class="flex items-center gap-2 md:ml-auto">
          <!-- Buttons for teacher/admin to accept or delete booking -->
          {#if !$selectedBookingStore.isAccepted}
            <Button onmousedown={handleDeleteBooking} disabled={loadingDelete} variant="outline" class="border-destructive text-destructive hover:bg-destructive/90 hover:text-white" size="sm">
              {#if loadingDelete}
                <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
              {:else}
                <Trash class="mr-2 h-4 w-4" />
              {/if}
              Delete
            </Button>
          {/if}

          {#if !$selectedBookingStore.isAccepted && userAuthed}
            <Button onmousedown={handleAcceptBooking} disabled={loadingAccept} variant="outline" class="border-green-600 text-green-600 hover:bg-green-600/90 hover:text-white" size="sm">
              {#if loadingAccept}
                <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
              {:else}
                <Check class="mr-2 h-4 w-4" />
              {/if}
              Accept
            </Button>
          {/if}

          {#if $selectedBookingStore.isAccepted}
            <VMActionsDropdown />
          {/if}
        </div>
      </div>

      <div class="grid gap-4 md:grid-cols-[1fr_250px] lg:grid-cols-3 lg:gap-8">
        <div class="grid auto-rows-max items-start gap-4 lg:col-span-2 lg:gap-8">
          <Card.Root>
            <Card.Header>
              <Card.Title>Details</Card.Title>
              <Card.Description>Information about the booking and it's owner</Card.Description>
            </Card.Header>
            <Card.Content>
              <div class="grid gap-6">
                <div class="grid gap-3">
                  <div class="inline-flex flex-wrap gap-x-7 gap-y-4 justify-between text-sm">
                    {#each users as { label, user }}
                      <div class="grid gap-3">
                        <Label for={label.toLowerCase()}>{label}</Label>
                        <a href="/user/{user.id}" class="flex gap-2 items-center hover:font-medium">
                          <Avatar.Root>
                            <Avatar.Fallback>{user.name[0]}{user.surname[0]}</Avatar.Fallback>
                          </Avatar.Root>
                          {user.name}
                          {user.surname}
                        </a>
                      </div>
                    {/each}
                  </div>
                </div>

                <div class="grid gap-3">
                  <Label for="description">Created and expire dates</Label>
                  <div class="flex gap-2 text-sm items-center">
                    <p>{formatDateTime($selectedBookingStore.createdAt)}</p>
                    <ArrowRight class="size-4" />
                    <p>{formatDateTime($selectedBookingStore.expiredAt)}</p>
                  </div>
                </div>

                <div class="text-sm mb-4">
                  <div>Machine UUID</div>
                  <div>
                    {$selectedBookingStore.uuid}
                  </div>
                </div>
                <div class="grid gap-3">
                  <Label for="description">Note</Label>
                  <Textarea class="resize-none" disabled value={$selectedBookingStore.message} />
                </div>
              </div>
            </Card.Content>
          </Card.Root>

          <Card.Root>
            <Card.Header>
              <Card.Title>Virtual machine</Card.Title>
              <Card.Description>Details about template and credentials</Card.Description>
            </Card.Header>
            <Card.Content>
              <div class="grid gap-6">
                <div class="inline-flex">
                  <div class="grid gap-3">
                    <Label for="vmType" class="ml-2 font-bold">Template</Label>
                    <div class="py-2 px-3 rounded-lg bg-secondary text-secondary-foreground text-sm">{$selectedBookingStore.type}</div>
                  </div>
                </div>

                <div class="grid gap-3">
                  <div class="inline-flex flex-wrap gap-x-7 gap-y-4">
                    <!-- VM ip -->
                    <div class="grid gap-3">
                      <Label for="vmIp" class="ml-2 font-bold">IP</Label>
                      {#if $selectedBookingStore.ip}
                        <button
                          class="py-2 px-3 rounded-lg bg-secondary text-secondary-foreground text-sm cursor-pointer hover:bg-secondary/80"
                          onmousedown={() => copyToClipboard($selectedBookingStore.ip)}>{$selectedBookingStore.ip}</button
                        >
                      {:else}
                        <Skeleton class="w-28 h-9 rounded-lg" />
                      {/if}
                    </div>

                    <!-- VM username -->
                    <div class="grid gap-3">
                      <Label for="vmUsername" class="ml-2 font-bold">Username</Label>
                      {#if $selectedBookingStore.username}
                        <button
                          class="py-2 px-3 rounded-lg bg-secondary text-secondary-foreground text-sm cursor-pointer hover:bg-secondary/80"
                          onmousedown={() => copyToClipboard($selectedBookingStore.username)}>{$selectedBookingStore.username}</button
                        >
                      {:else}
                        <Skeleton class="w-20 h-9 rounded-lg" />
                      {/if}
                    </div>

                    <!-- VM password -->
                    <div class="grid gap-3">
                      <Label for="vmpassword" class="ml-2 font-bold">Password</Label>
                      {#if $selectedBookingStore.password}
                        <button
                          class="py-2 px-3 rounded-lg bg-secondary text-secondary-foreground text-sm cursor-pointer hover:bg-secondary/80"
                          onmousedown={() => copyToClipboard($selectedBookingStore.password)}>{$selectedBookingStore.password}</button
                        >
                      {:else}
                        <Skeleton class="w-24 h-9 rounded-lg" />
                      {/if}
                    </div>
                  </div>
                </div>
              </div>
            </Card.Content>
          </Card.Root>
        </div>
        <div class="flex flex-col w-full items-start gap-4 lg:gap-8">
          {#each metrics as metric}
            <Card.Root class="w-full">
              <Card.Header>
                <Card.Title>{metric} usage</Card.Title>
              </Card.Header>
              <Card.Content>
                {#if vmData?.usageInfo?.length}
                  {@const lastEntry = vmData.usageInfo[vmData.usageInfo.length - 1] || {}}

                  {#if metric === 'CPU'}
                    <div class="flex items-center gap-4">
                      <div>
                        <div class="text-2xl font-bold text-primary">
                          {((lastEntry?.cpu || 0) * 100).toFixed(2)}%
                        </div>
                        <div>
                          <p class="text-xs">
                            {lastEntry?.maxcpu || 0} Cores
                          </p>
                        </div>
                      </div>
                      <div class="flex-1">
                        <CPUUsageChart usageInfo={vmData.usageInfo} />
                      </div>
                    </div>
                  {:else if metric === 'Memory'}
                    <div class="flex items-center gap-4">
                      <div>
                        <div class="text-2xl font-bold text-primary">
                          {(((lastEntry?.mem || 0) / (lastEntry?.maxmem || 1)) * 100).toFixed(2)}%
                        </div>
                        <div class="flex flex-col text-xs">
                          <p>{((lastEntry?.mem || 0) / (1024 * 1024 * 1024)).toFixed(2)} GB</p>
                          <p>/</p>
                          <p>{((lastEntry?.maxmem || 0) / (1024 * 1024 * 1024)).toFixed(2)} GB</p>
                        </div>
                      </div>
                      <div class="flex-1">
                        <RamUsageChart usageInfo={vmData.usageInfo} />
                      </div>
                    </div>
                  {:else if metric === 'Disk'}
                    <div class="flex items-center gap-4">
                      <div>
                        <div class="text-2xl font-bold text-primary">
                          {((lastEntry?.diskwrite || 0) / 1024).toFixed(2)} KB/s
                        </div>
                        <div class="text-xs">
                          <p>{((lastEntry?.maxdisk || 0) / (1024 * 1024 * 1024)).toFixed(2)} GB</p>
                          <p>Space total</p>
                        </div>
                      </div>
                      <div class="flex-1">
                        <DiskWriteChart usageInfo={vmData.usageInfo} />
                      </div>
                    </div>
                  {/if}
                {:else}
                  <div class="p-4 text-center text-muted-foreground">No data available</div>
                {/if}
              </Card.Content>
            </Card.Root>
          {/each}
        </div>
      </div>
    </div>
  {/if}
</main>
