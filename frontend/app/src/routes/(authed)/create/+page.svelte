<script>
  import { vmService } from '$lib/services/vm-service';
  import { userStore } from '$lib/utils/store';
  import { goto } from '$app/navigation';
  import { CalendarDate, today, getLocalTimeZone } from '@internationalized/date';
  import { ChevronLeft, ChevronDown, LoaderCircle, CalendarIcon } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';
  import { cn } from '$lib/utils.js';
  import { Badge } from '$lib/components/ui/badge';
  import { Button } from '$lib/components/ui/button';
  import * as Card from '$lib/components/ui/card';
  import { Calendar } from '$lib/components/ui/calendar';
  import * as Popover from '$lib/components/ui/popover';
  import * as Select from '$lib/components/ui/select/index.js';
  import { Textarea } from '$lib/components/ui/textarea/index.js';
  import { Label } from '$lib/components/ui/label';
  import * as Command from '$lib/components/ui/command/index.js';

  let { data } = $props();

  userStore.set(data.userInfo);
  let userAuthed = $derived(data.userInfo.role !== 'Student');
  let isLoading = $state(false);

  // Form input state for VM bookings
  let vmBookingInput = $state({
    type: null,
    ownerId: null,
    assignedId: null,
    message: null,
    expiringAt: null
  });

  let vmCalendarDatePicked = $state(null);
  let vmCalendarDateFormated = $derived(new Date(vmCalendarDatePicked).toLocaleDateString(undefined, { dateStyle: 'long' }));

  let selectedStudent = $state(null);
  let selectedTeacher = $state(null);
  let selectStudentOpen = $state(false);
  let selectTeacherOpen = $state(false);

  function handleStudentSelect(student) {
    const [name, surname, id] = student.split(' ');
    const parsedId = +id;

    if (parsedId === vmBookingInput.ownerId) {
      selectedStudent = null;
      vmBookingInput.ownerId = null;
    } else {
      vmBookingInput.ownerId = parsedId;
      selectedStudent = `${name} ${surname}`;
    }
    selectStudentOpen = false;
  }

  function handleTeacherSelect(teacher) {
    const [name, surname, id] = teacher.split(' ');
    const parsedId = +id;

    if (parsedId === vmBookingInput.assignedId) {
      selectedTeacher = null;
      vmBookingInput.assignedId = null;
    } else {
      vmBookingInput.assignedId = parsedId;
      selectedTeacher = `${name} ${surname}`;
    }
    selectTeacherOpen = false;
  }

  // Creates a new VM booking after validating required fields
  async function handleCreateVMBoooking() {
    if (!vmBookingInput.ownerId) {
      vmBookingInput.ownerId = $userStore.id;
    }

    if (userAuthed && !vmBookingInput.assignedId) {
      vmBookingInput.assignedId = $userStore.id;
    }

    if (!vmBookingInput.type) {
      toast.error(`Please select a VM template`);
      return;
    } else if (!userAuthed && !vmBookingInput.assignedId) {
      toast.error(`Please assign the booking to your teacher`);
      return;
    } else if (!vmCalendarDatePicked) {
      toast.error(`Please select an expire date`);
      return;
    } else if (!vmBookingInput.message) {
      toast.error(`Please add a comment describing the purpose of the booking`);
      return;
    }

    try {
      isLoading = true;
      vmBookingInput.expiringAt = new Date(vmCalendarDatePicked).toISOString();
      await vmService.createVMBooking(vmBookingInput);
      toast.success(`VM booking created`);
      history.back();
    } catch (error) {
      toast.error(error.message);
    } finally {
      isLoading = false;
    }
  }
</script>

<main class="grid flex-1 items-start gap-4 p-4 sm:px-6 md:gap-8">
  <div class="mx-auto grid flex-1 auto-rows-max gap-4">
    <div class="flex flex-wrap items-center gap-4">
      <Button href="/" variant="outline" size="icon" class="size-7">
        <ChevronLeft class="size-4" />
        <span class="sr-only">Back</span>
      </Button>
      <h1 class="flex-1 shrink-0 whitespace-nowrap text-xl font-semibold tracking-tight sm:grow-0">Create booking</h1>
    </div>
    <div class="grid gap-4 lg:gap-8">
      <div class="grid auto-rows-max items-start gap-4 lg:gap-8 lg:max-w-md">
        <Card.Root>
          <Card.Header>
            <Card.Title>Book a virtual machine</Card.Title>
            <Card.Description
              >Book a virtual machine with custom configuration and duration. Make sure to read the
              <a href="https://mars.merhot.dk/w/index.php/Booking-guide" target="_blank" class="text-primary underline">Booking Guide</a></Card.Description
            >
          </Card.Header>
          <Card.Content>
            <form
              onsubmit={(e) => {
                e.preventDefault();
                handleCreateVMBoooking();
              }}
              class="grid items-center py-4 gap-4"
            >
              <div class="grid gap-2">
                <div class="flex justify-between items-center gap-x-4">
                  <Label for="studentCount">Virtual machine templates</Label>
                  <div>
                    <Badge class="w-auto">? VMs available</Badge>
                  </div>
                </div>

                <!-- Select VM template component -->
                <Select.Root type="single" bind:value={vmBookingInput.type}>
                  <Select.Trigger>
                    {vmBookingInput.type ? data.vmTemplates.find((t) => t.internalName === vmBookingInput.type)?.displayName : 'Select a template'}
                  </Select.Trigger>
                  <Select.Content>
                    <Select.Group class="overflow-y-scroll">
                      {#each data.vmTemplates as vmTemplate}
                        <Select.Item value={vmTemplate.internalName}>{vmTemplate.displayName}</Select.Item>
                      {/each}
                    </Select.Group>
                  </Select.Content>
                </Select.Root>
              </div>

              <!-- Select student component -->
              {#if userAuthed}
                <div class="grid gap-2">
                  <div class="flex justify-between items-center">
                    <Label for="studentCount">Assign to student</Label>
                  </div>
                  <Popover.Root bind:open={selectStudentOpen}>
                    <Popover.Trigger>
                      <Button variant="outline" class="justify-between px-3 w-full {selectedStudent ?? 'text-muted-foreground'}">
                        {selectedStudent ?? 'Select a student'}
                        <ChevronDown class="h-4 w-4" />
                      </Button>
                    </Popover.Trigger>
                    <Popover.Content class="p-0" align="end">
                      <Command.Root>
                        <Command.Input placeholder="Select a student..." />
                        <Command.List>
                          <Command.Empty>No students found.</Command.Empty>
                          <Command.Group>
                            {#each data.listOfUsers as user (user.id)}
                              {#if user.role === 'Student'}
                                <Command.Item
                                  value={`${user.name} ${user.surname} ${user.id}`}
                                  onmousedown={() => {
                                    handleStudentSelect(`${user.name} ${user.surname} ${user.id}`);
                                  }}
                                  class="items-start px-4 py-2"
                                >
                                  <p>{user.name} {user.surname}</p>
                                </Command.Item>
                              {/if}
                            {/each}
                          </Command.Group>
                        </Command.List>
                      </Command.Root>
                    </Popover.Content>
                  </Popover.Root>
                </div>
              {:else}
                <!-- Select teacher component -->
                <div class="grid gap-2">
                  <div class="flex justify-between items-center">
                    <Label for="teacherCount">Assign to teacher</Label>
                  </div>
                  <Popover.Root bind:open={selectTeacherOpen}>
                    <Popover.Trigger>
                      <Button variant="outline" class="justify-between px-3 w-full {selectedTeacher ?? 'text-muted-foreground'}">
                        {selectedTeacher ?? 'Select a teacher'}
                        <ChevronDown class="h-4 w-4" />
                      </Button>
                    </Popover.Trigger>
                    <Popover.Content class="p-0" align="end">
                      <Command.Root>
                        <Command.Input placeholder="Select a teacher..." />
                        <Command.List>
                          <Command.Empty>No teachers found.</Command.Empty>
                          <Command.Group>
                            {#each data.listOfUsers as user (user.id)}
                              {#if user.role === 'Teacher'}
                                <Command.Item
                                  value={`${user.name} ${user.surname} ${user.id}`}
                                  onmousedown={() => handleTeacherSelect(`${user.name} ${user.surname} ${user.id}`)}
                                  class="items-start px-4 py-2"
                                >
                                  <p>{user.name} {user.surname}</p>
                                </Command.Item>
                              {/if}
                            {/each}
                          </Command.Group>
                        </Command.List>
                      </Command.Root>
                    </Popover.Content>
                  </Popover.Root>
                </div>
              {/if}

              <div class="grid gap-2">
                <Label for="dayCount">Booking expire date</Label>
                <!-- Date picker -->
                <Popover.Root>
                  <Popover.Trigger>
                    <Button variant="outline" class={cn('justify-start text-left font-normal w-full', !vmCalendarDatePicked && 'text-muted-foreground')}>
                      <CalendarIcon class="mr-2 h-4 w-4" />
                      {vmCalendarDatePicked ? vmCalendarDateFormated : 'Pick a date'}
                    </Button>
                  </Popover.Trigger>
                  <Popover.Content class="w-auto p-0">
                    <Calendar
                      bind:value={vmCalendarDatePicked}
                      initialFocus
                      calendarLabel="Booking expire date"
                      minValue={new CalendarDate(today(getLocalTimeZone()).year, today(getLocalTimeZone()).month, today(getLocalTimeZone()).day + 1)}
                      maxValue={new CalendarDate(
                        today(getLocalTimeZone()).year + (today(getLocalTimeZone()).month + 6 > 12 ? 1 : 0),
                        (today(getLocalTimeZone()).month + 6) % 12 || 12,
                        today(getLocalTimeZone()).day
                      )}
                    />
                  </Popover.Content>
                </Popover.Root>
              </div>

              <div class="grid w-full gap-1.5">
                <Label for="comment">Comment about booking</Label>
                <Textarea class="max-h-20" placeholder="Describe the purpose of this VM booking" id="comment" bind:value={vmBookingInput.message} />
              </div>

              <Button type="submit" disabled={isLoading}
                >{#if isLoading}
                  <LoaderCircle class="mr-2 h-4 w-4 animate-spin" />
                {/if}
                Create booking</Button
              >
            </form>
          </Card.Content>
        </Card.Root>
      </div>
    </div>
  </div>
</main>
