using System;
using System.Threading;

namespace transf
{
	/// <summary>
	/// An abstract class that a worker thread can derive from and easily be started. 
	/// </summary>
	public abstract class WorkerThread
	{
		protected bool StopSignal { get; private set; }

		private Thread workThread;

		public WorkerThread ()
		{
			workThread = new Thread(Run);
		}

		/// <summary>
		/// Starts the worker thread.
		/// </summary>
		/// <param name="args">Arguments for the specific worker</param>
		/// <returns><c>true</c> if the thread is started, otherwise false</returns>
		public bool Start(params object[] args)
		{
			if (IsRunning())
				return false;
			workThread = new Thread (Run);
			StopSignal = false;
            if (!Initialize(args))
                return false;
			workThread.Start ();
			return true;
		}

		/// <summary>
		/// Stops the worker thread. This will send the stop signal and join the thread until
		/// it has completed.
		/// </summary>
		/// <returns><c>true</c> if the thread was stopped successfully, <c>false</c> otherwise</returns>
		public bool Stop()
		{
			if (!IsRunning())
				return false;
			StopSignal = true;
			if (workThread.IsAlive)
				workThread.Join ();
			return true;
		}

		/// <summary>
		/// Joins the active worker thread into the current thread.
		/// </summary>
		public void Join()
		{
			if (StopSignal)
				return;
			if (workThread.IsAlive)
				workThread.Join ();
		}

		public bool IsRunning()
		{
			return !StopSignal && workThread.IsAlive;
		}

		/// <summary>
		/// The abstract run method that does the work under the workerthread.
		/// </summary>
		/// <param name="args">Specific arguments for the run method.</param>
		protected abstract void Run();

        /// <summary>
        /// Initializes any resources and returns a success result.
        /// </summary>
        /// <param name="args">Any arguments for the worker to initialize itself.</param>
        /// <returns><c>true</c> if initialization was successful, otherwise <c>false</c>.</returns>
        protected abstract bool Initialize(params object[] args);
	}
}

