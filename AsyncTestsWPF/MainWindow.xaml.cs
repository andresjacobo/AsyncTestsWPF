using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AsyncTestsWPF
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
    }

    private static async Task<string> DoIOWorkAsync()
    {
      //Here we just fetch the Google homepage - it should take just a few ms.
      using (HttpClient client = new HttpClient())
      {
        var response = await client.GetAsync("https://google.com"); //At this point, control is returned to the caller and the current thread is freed to do other work.
        string html = await response.Content.ReadAsStringAsync();
        return html;
      }
    }

    private static Task DoCPUWorkAsync()
    {
      //Here we just add numbers up simulating CPU work. It should finish in about 1sec.
      return Task.Run(() =>
      {
        int x = 0;
        
        for (int i = 0; i < 10; i++)
        {
          x += i;
          Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }

      });
    }

    private static async Task SleepAsync()
    {
      //Here we just sleep for a total of about 1sec.
      for (int i = 0; i < 10; i++)
      {
        await Task.Delay(TimeSpan.FromMilliseconds(100)); //At this point, control is returned to the caller and the current thread is freed to do other work.
      }
    }

    private void btnMainThreadDeadlock_Click(object sender, RoutedEventArgs e)
    {
      //Step 1. Do IO work async.
      Task<string> task = DoIOWorkAsync();

      //Step . Wait/.Result the task.
      string html = task.Result; //Deadlock! .Wait() has the same effect.

      //Step 4. Sadly, this step will never be reached.
      // Why? DoIOWorkAsync was called from the current thread, so the runtime will ensure it resumes on the same thread once the async operation completes.
      // However, the current thread is blocked on task.Wait() waiting for itself to finish. Infinite deadlock.
      MessageBox.Show("We're Done!");
    }

    private void btnMainThreadDeadlockCPU_Click(object sender, RoutedEventArgs e)
    {
      //Step 1. Start CPU work async (This runs on a separate thread pool thread)
      Task cpuTask = DoCPUWorkAsync();
      
      //Step 2. Do IO Work async.
      Task ioTask = DoIOWorkAsync();

      //Step 3. Wait/.Result the task.
      Task.WaitAll(cpuTask, ioTask); //Deadlock!

      //Step 4. Sadly, this step will never be reached.
      //Why? Same as btnMainThreadDeadlock_Click. If there is just a *single* IO bound task in the mix, you'll deadlock forever.
      MessageBox.Show("We're Done!");
    }

    private void btnMainThreadDeadlockCPUDelay_Click(object sender, RoutedEventArgs e)
    {
      //Step 1. Start CPU work async (This runs on a separate thread pool thread)
      Task cpuTask = DoCPUWorkAsync();

      //Step 2. Sleep async.
      Task sleepTask = SleepAsync();

      //Step 3. Wait/.Result the task.
      Task.WaitAll(cpuTask, sleepTask); //Deadlock!

      //Step 4. Sadly, this step will never be reached.
      //Why? Same as btnMainThreadDeadlock_Click. If there is just a *single* pure async task in the mix, it doesn't even matter if it isn't IO-bound (It could be an async semaphore), you'll deadlock forever. 
      MessageBox.Show("We're Done!");
    }

    private async void btnMainThreadNoDeadlock_Click(object sender, RoutedEventArgs e)
    {
      //Step 1. Start CPU work async (This runs on a separate thread pool thread)
      Task cpuTask = DoCPUWorkAsync();

      //Step 2. Sleep async.
      Task sleepTask = SleepAsync();

      //Step 3. Do IO work async.
      Task ioTask = DoIOWorkAsync();

      //Step 3. Wait/.Result the task.
      await Task.WhenAll(cpuTask, sleepTask, ioTask);

      //Step 4. Yay! We get to this step this time.
      //Why? Unlike WaitAll(), WhenAll() returns the control back to the caller and frees up the main thread so it can be re-used in the pure async tasks. 
      //The CPU bound task is efficiently scheduled by the task scheduler aware of the pure-async task.
      MessageBox.Show("We're Done!");
    }
  }
}
