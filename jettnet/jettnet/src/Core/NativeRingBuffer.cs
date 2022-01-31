using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace jettnet {
    /// <summary>
    /// Single producer, single consumer, FIFO thread safe ring buffer
    /// </summary>
    public unsafe class NativeRingBuffer {

        private SpinLock spinLock;

        private void* buffer;
        private void* readPointer;
        private void* writePointer;
        private void* tail;

        private int elementSize;
        private int elementCount;

        public NativeRingBuffer(int elementSize, int elementCount) {
            this.elementSize = elementSize;
            this.elementCount = elementCount + 1; // plus 1 since there is always one element between read and write pointer which cant be written to

            buffer = (void*) Marshal.AllocHGlobal(this.elementSize * this.elementCount);
            writePointer = buffer;
            readPointer = buffer;
            tail = (byte*) buffer + (this.elementSize * this.elementCount);
        }

        /// <summary>
        /// Reserves a new element in the buffer
        /// </summary>
        /// <returns>Returns a memory location in the buffer or null if buffer is full</returns>
        public void* Reserve() {
            void* writePosition = null;

            bool lockTaken = false;
            try {
                spinLock.Enter(ref lockTaken);

                // Get next element position
                void* nextElement = (byte*) writePointer + elementSize;
                if (nextElement == tail) {
                    nextElement = buffer;
                }

                // Check if the buffer is full
                if (nextElement == readPointer) {
                    return null;
                }

                writePosition = writePointer;

                // Advance write pointer
                writePointer = nextElement;
            } finally {
                if (lockTaken) spinLock.Exit(false);
            }

            return writePosition;
        }

        /// <summary>
        /// Reserves a new element in the buffer, sleeps if buffer is full until space is available
        /// </summary>
        /// <param name="sleepInterval">how long thread will sleep if buffer is full</param>
        /// <returns>Returns a memory location in the buffer</returns>
        public void* SleepReserve(int sleepInterval = 1) {
            void* writePosition = null;

            do {
                bool lockTaken = false;
                try {
                    spinLock.Enter(ref lockTaken);

                    // Get next element position
                    void* nextElement = (byte*) writePointer + elementSize;
                    if (nextElement == tail) {
                        nextElement = buffer;
                    }

                    // Check if the buffer is full
                    if (nextElement == readPointer) {
                        continue;
                    }

                    writePosition = writePointer;

                    // Advance write pointer
                    writePointer = nextElement;
                } finally {
                    if (lockTaken) spinLock.Exit(false);

                    // spin wait and try again if pointer is null
                    if (writePosition == null) {
                        Thread.Sleep(sleepInterval);
                    }
                }
            } while (writePosition == null);

            return writePosition;
        }

        /// <summary>
        /// Returns a memory location in the buffer to the next element that can be read
        /// </summary>
        /// <returns>Returns a memory location in the buffer or null if buffer is empty</returns>
        public void* Read() {
            void* readPosition = null;

            bool lockTaken = false;
            try {
                spinLock.Enter(ref lockTaken);

                // Get next element position
                void* nextElement = (byte*) readPointer + elementSize;
                if (nextElement == tail) {
                    nextElement = buffer;
                }

                // Check if the buffer is empty, checks if the next element is the write pointer
                if (nextElement == writePointer) {
                    return null;
                }

                readPosition = readPointer;

                // Advance read pointer
                readPointer = nextElement;
            } finally {
                if (lockTaken) spinLock.Exit(false);
            }


            return readPosition;
        }

        /// <summary>
        /// Returns a memory location in the buffer to the next element that can be read 
        /// and sleeps if the buffer is empty until something can be read
        /// </summary>
        /// <param name="sleepInterval">how long thread will sleep if buffer is empty</param>
        /// <returns>Returns a memory location in the buffer</returns>
        public void* SleepRead(int sleepInterval = 1) {
            void* readPosition = null;

            do {
                bool lockTaken = false;
                try {
                    spinLock.Enter(ref lockTaken);

                    // Get next element position
                    void* nextElement = (byte*) readPointer + elementSize;
                    if (nextElement == tail) {
                        nextElement = buffer;
                    }

                    // Check if the buffer is empty, checks if the next element is the write pointer
                    if (nextElement == writePointer) {
                        continue;
                    }

                    readPosition = readPointer;

                    // Advance read pointer
                    readPointer = nextElement;
                } finally {
                    if (lockTaken) spinLock.Exit(false);

                    // spin wait and try again if pointer is null
                    if (readPosition == null) {
                        Thread.Sleep(sleepInterval);
                    }
                }
            } while (readPosition == null);

            return readPosition;
        }
    }
}