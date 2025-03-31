<script>
  import UserInfo from '$lib/components/authed/user/user-info.svelte';
  import BookingList from '$lib/components/authed/bookings/booking-list.svelte';
  import { vmListStore, userStore } from '$lib/utils/store';
  import { toast } from 'svelte-sonner';
  import { goto, afterNavigate } from '$app/navigation';

  const { data } = $props();

  userStore.set(data.userInfo);
  vmListStore.set(data.vmData);

  let userAuthed = $derived($userStore.role === 'Admin' || $userStore.role === 'Teacher');

  // Function to check for and display errors
  function checkErrors() {
    if (data.errorMessage || !data.userData) {
      toast.error(data.errorMessage);

      if (data.errorMessage === 'Invalid user ID' || data.errorMessage === 'User not found') {
        goto('/');
      }
    }

    // Show toast if user has no bookings
    if (userAuthed && $vmListStore.length === 0) {
      toast.error(`User has no bookings`);
    }
  }

  afterNavigate(() => {
    checkErrors();
  });
</script>

<main class="flex flex-col flex-grow rounded-3xl bg-background p-4 lg:p-6 gap-y-6">
  <div class="mx-auto mt-10">
    {#if data.userData}
      <UserInfo user={data.userData} />
    {/if}
  </div>

  {#if data.vmData && !data.errorMessage}
    <div class="flex flex-grow flex-col">
      <!-- List of all bookings -->
      <BookingList />
    </div>
  {:else if data.errorMessage === 'Insufficient privileges to perform this action'}
    <div class="flex-grow flex justify-center items-center">
      <div class="flex flex-col gap-y-6 items-center justify-center">
        <p class="text-muted-foreground select-none">You are not authorized to view this user's bookings</p>
      </div>
    </div>
  {/if}
</main>
