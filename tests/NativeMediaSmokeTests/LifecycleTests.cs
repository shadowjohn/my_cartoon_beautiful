using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using my_cartoon_beautiful;
internal static class LifecycleTests
{
    public static void Run()
    {
        var jobField=typeof(Form1).GetField("activeJob",BindingFlags.Instance|BindingFlags.NonPublic);
        if(jobField==null)throw new Exception("Form must retain active native job until cleanup finishes");
        Exception error=null;
        var ui=new Thread(()=>{
            try {
                using(var form=new Form1())using(var cts=new CancellationTokenSource()) {
                    var completion=new TaskCompletionSource<bool>();
                    form.Shown+=async (s,e)=>{
                        try {
                            jobField.SetValue(form,completion.Task);
                            typeof(Form1).GetField("cts",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,cts);
                            cts.Cancel();form.Close();
                            await Task.Delay(100);
                            if(form.IsDisposed)throw new Exception("Form closed before native task finished");
                            completion.SetResult(true);
                        } catch(Exception ex){error=ex;form.Dispose();Application.ExitThread();}
                    };
                    Application.Run(form);
                    if(!completion.Task.IsCompleted)throw new Exception("Native task was abandoned");
                }
            } catch(Exception ex){error=ex;}
        });
        ui.SetApartmentState(ApartmentState.STA);ui.Start();
        if(!ui.Join(10000))throw new Exception("UI close did not finish cooperatively");
        if(error!=null)throw error;
        Console.WriteLine("NativeMediaSmokeTests: PASS [lifecycle close waits for native job]");
    }
}
