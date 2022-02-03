using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace jettnet 
{
    /// <summary>
    /// Single producer, single consumer, FIFO thread safe ring buffer
    /// </summary>
    public unsafe class NativeRingBuffer 
    {

        private SpinLock _spinLock;

        private void* _buffer;
        private void* _readPointer;
        private void* _writePointer;
        private void* _tail;

        private int _elementSize;
        private int _elementCount;

        public NativeRingBuffer(int elementSize, int elementCount) 
        {
            this._elementSize = elementSize;
            this._elementCount = elementCount + 1; // plus 1 since there is always one element between read and write pointer which cant be written to

            _buffer = (void*) Marshal.AllocHGlobal(this._elementSize * this._elementCount);
            _writePointer = _buffer;
            _readPointer = _buffer;
            _tail = (byte*) _buffer + (this._elementSize * this._elementCount);
        }

        /// <summary>
        /// Reserves a new element in the buffer
        /// </summary>
        /// <returns>Returns a memory location in the buffer or null if buffer is full</returns>
        public void* Reserve() 
        {
            void* writePosition = null;

            bool lockTaken = false;
            try 
            {
                _spinLock.Enter(ref lockTaken);

                // Get next element position
                void* nextElement = (byte*) _writePointer + _elementSize;
                if (nextElement == _tail) 
                {
                    nextElement = _buffer;
                }

                // Check if the buffer is full
                if (nextElement == _readPointer) 
                {
                    return null;
                }

                writePosition = _writePointer;

                // Advance write pointer
                _writePointer = nextElement;
            } 
            finally 
            {
                if (lockTaken)
                {
                    _spinLock.Exit(false);
                }
            }

            return writePosition;
        }

        /// <summary>
        /// Reserves a new element in the buffer, sleeps if buffer is full until space is available
        /// </summary>
        /// <param name="sleepInterval">how long thread will sleep if buffer is full</param>
        /// <returns>Returns a memory location in the buffer</returns>
        public void* SleepReserve(int sleepInterval = 1) 
        {
            void* writePosition = null;

            do 
            {
                bool lockTaken = false;
                try 
                {
                    _spinLock.Enter(ref lockTaken);

                    // Get next element position
                    void* nextElement = (byte*) _writePointer + _elementSize;
                    if (nextElement == _tail) 
                    {
                        nextElement = _buffer;
                    }

                    // Check if the buffer is full
                    if (nextElement == _readPointer) 
                    {
                        continue;
                    }

                    writePosition = _writePointer;

                    // Advance write pointer
                    _writePointer = nextElement;
                } 
                finally 
                {
                    if (lockTaken)
                    { 
                        _spinLock.Exit(false);
                    }

                    // spin wait and try again if pointer is null
                    if (writePosition == null) {
                        Thread.Sleep(sleepInterval);
                    }
                }
            } 
            while (writePosition == null);

            return writePosition;
        }

        /// <summary>
        /// Returns a memory location in the buffer to the next element that can be read
        /// </summary>
        /// <returns>Returns a memory location in the buffer or null if buffer is empty</returns>
        public void* Read() 
        {
            void* readPosition = null;

            bool lockTaken = false;
            try 
            {
                _spinLock.Enter(ref lockTaken);

                // Get next element position
                void* nextElement = (byte*) _readPointer + _elementSize;
                if (nextElement == _tail) 
                {
                    nextElement = _buffer;
                }

                // Check if the buffer is empty, checks if the next element is the write pointer
                if (nextElement == _writePointer) 
                {
                    return null;
                }

                readPosition = _readPointer;

                // Advance read pointer
                _readPointer = nextElement;
            } 
            finally 
            {
                if (lockTaken) 
                { 
                    _spinLock.Exit(false);
                }
            }


            return readPosition;
        }

        /// <summary>
        /// Returns a memory location in the buffer to the next element that can be read 
        /// and sleeps if the buffer is empty until something can be read
        /// </summary>
        /// <param name="sleepInterval">how long thread will sleep if buffer is empty</param>
        /// <returns>Returns a memory location in the buffer</returns>
        public void* SleepRead(int sleepInterval = 1) 
        {
            void* readPosition = null;

            do 
            {
                bool lockTaken = false;
                try 
                {
                    _spinLock.Enter(ref lockTaken);

                    // Get next element position
                    void* nextElement = (byte*) _readPointer + _elementSize;
                    if (nextElement == _tail) 
                    {
                        nextElement = _buffer;
                    }

                    // Check if the buffer is empty, checks if the next element is the write pointer
                    if (nextElement == _writePointer) 
                    {
                        continue;
                    }

                    readPosition = _readPointer;

                    // Advance read pointer
                    _readPointer = nextElement;
                } 
                finally 
                {
                    if (lockTaken)
                    { 
                        _spinLock.Exit(false);
                    }

                    // spin wait and try again if pointer is null
                    if (readPosition == null) 
                    {
                        Thread.Sleep(sleepInterval);
                    }
                }
            } 
            while (readPosition == null);

            return readPosition;
        }
    }
}