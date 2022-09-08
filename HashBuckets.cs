//using System.Collections;
//using System.Runtime.InteropServices;

//namespace NetMph;

//internal sealed unsafe class HashBuckets : IDisposable
//{
//    private const uint keysPerBucket = 4; // average number of keys per bucket
//    private const uint maxProbesBase = 1 << 20;

//    private struct Item
//    {
//        public uint F;
//        public uint H;
//    }

//    private struct Bucket
//    {
//        public uint ItemsList; // offset

//        public uint Size { get; set; }

//        public uint BucketId
//        {
//            get => this.Size;
//            set => this.Size = value;
//        }
//    }

//    private struct MapItem
//    {
//        public uint F;
//        public uint H;
//        public uint BucketNum;
//    };

//    ~HashBuckets() => this.Dispose(false);

//    private void Dispose(bool isDisposing)
//    {
//        NativeMemory.Free(this.buckets);
//        NativeMemory.Free(this.items);
//    }

//    public void Dispose()
//    {
//        this.Dispose(true);
//        GC.SuppressFinalize(this);
//    }

//    private Bucket* buckets;
//    private Item* items;
//    private readonly uint keyCount; // number of keys
//    private readonly IKeySource keySource;

//    public uint BucketCount { get; }

//    /// <summary>
//    /// Gets the number of bins.
//    /// </summary>
//    /// <value>
//    /// The n.
//    /// </value>
//    public uint BinCount { get; }

//    public HashBuckets(IKeySource keySource, double c, Action<string> output)
//    {
//        this.keySource = keySource;

//        var loadFactor = c;
//        this.keyCount = keySource.KeyCount;
//        this.BucketCount = this.keyCount / HashBuckets.keysPerBucket + 1;

//        output?.Invoke($"Buckets: {this.BucketCount}\n");

//        if (loadFactor < 0.5)
//            loadFactor = 0.5;
//        if (loadFactor >= 0.99)
//            loadFactor = 0.99;

//        this.BinCount = (uint)(this.keyCount / loadFactor) + 1;

//        output?.Invoke(this.BinCount + " bins requested\n");

//        if (this.BinCount % 2 == 0)
//            this.BinCount++;
//        for (; ; )
//        {
//            if (MillerRabin.CheckPrimality(this.BinCount))
//                break;
//            this.BinCount += 2; // just odd numbers can be primes for n > 2
//        }

//        output?.Invoke(this.BinCount + " bins needed\n");
//        this.buckets = new Bucket[this.BucketCount];
//        this.items = new Item[this.keyCount];
//    }

//    private bool BucketsInsert(MapItem* mapItems, uint itemIdx)
//    {
//        var tempMapItem = mapItems + itemIdx;
//        var bucket = this.buckets + tempMapItem->BucketNum;
//        var tempItem = this.items + bucket->ItemsList;

//        for (uint i = 0; i < bucket->Size; i++)
//        {
//            if (tempItem->F == tempMapItem->F && tempItem->H == tempMapItem->H)
//                return false;
//            tempItem++;
//        }
//        tempItem->F = tempMapItem->F;
//        tempItem->H = tempMapItem->H;
//        bucket->Size++;
//        return true;
//    }

//    private void BucketsClean()
//    {
//        for (uint i = 0; i < this.BucketCount; i++)
//            this.buckets[i].Size = 0;
//    }

//    /// <summary>
//    /// computes the entropy of non empty buckets.
//    /// </summary>
//    /// <param name="dispTable"></param>
//    /// <param name="n"></param>
//    /// <param name="maxProbes"></param>
//    /// <returns></returns>
//    private static double GetEntropy(uint* dispTable, uint n, uint maxProbes)
//    {
//        var probeCounts = (uint*)NativeMemory.Alloc(maxProbes, sizeof(uint));
//        var entropy = 0d;
//        try
//        {
//            for (var i = 0; i < n; i++) probeCounts[dispTable[i]]++;


//            for (var i = 0; i < maxProbes; i++)
//            {
//                if (probeCounts[i] > 0)
//                    entropy -= probeCounts[i] * Math.Log((double)probeCounts[i] / n) / Math.Log(2);
//            }
//        }
//        finally
//        {
//            NativeMemory.Free(probeCounts);
//        }
//        return entropy;
//    }

//    private static double SpaceLowerBound(uint n, uint r) => (1 + ((double)r / n - 1d + 1d / (2d * n)) * Math.Log(1 - (double)n / r)) / Math.Log(2);

//    public bool MappingPhase(out uint hashSeed, out uint maxBucketSize)
//    {
//        var hl = stackalloc uint[3];
//        var mapItems = new MapItem[this.keyCount];
//        uint mappingIterations = 1000;
//        var rdm = new Random(111);

//        maxBucketSize = 0;
//        for (; ; )
//        {
//            mappingIterations--;
//            hashSeed = (uint)rdm.Next((int)this.keyCount); // ((cmph_uint32)rand() % this->_m);

//            this.BucketsClean();

//            this.keySource.Rewind();

//            uint i;
//            for (i = 0; i < this.keyCount; i++)
//            {
//                JenkinsHash.HashVector(hashSeed, this.keySource.Read(), hl);

//                var g = hl[0] % this.BucketCount;
//                mapItems[i].F = hl[1] % this.BinCount;
//                mapItems[i].H = hl[2] % (this.BinCount - 1) + 1;
//                mapItems[i].BucketNum = g;

//                this.buckets[g].Size++;
//                if (this.buckets[g].Size > maxBucketSize)
//                    maxBucketSize = this.buckets[g].Size;
//            }
//            this.buckets[0].ItemsList = 0;
//            for (i = 1; i < this.BucketCount; i++)
//            {
//                this.buckets[i].ItemsList = this.buckets[i - 1].ItemsList + this.buckets[i - 1].Size;
//                this.buckets[i - 1].Size = 0;
//            }
//            this.buckets[i - 1].Size = 0;
//            for (i = 0; i < this.keyCount; i++)
//                if (!this.BucketsInsert(mapItems, i))
//                    break;
//            if (i == this.keyCount)
//                return true; // SUCCESS

//            if (mappingIterations == 0)
//                return false;
//        }
//    }

//    public BucketSortedList[] OrderingPhase(uint maxBucketSize)
//    {
//        var sortedLists = new BucketSortedList[maxBucketSize + 1];
//        var inputBuckets = this.buckets;
//        var inputItems = this.items;
//        uint i;
//        uint bucketSize, position;

//        for (i = 0; i < this.BucketCount; i++)
//        {
//            bucketSize = inputBuckets[i].Size;
//            if (bucketSize == 0)
//                continue;
//            sortedLists[bucketSize].Size++;
//        }

//        sortedLists[1].BucketsList = 0;
//        // Determine final position of list of buckets into the contiguous array that will store all the buckets
//        for (i = 2; i <= maxBucketSize; i++)
//        {
//            sortedLists[i].BucketsList = sortedLists[i - 1].BucketsList + sortedLists[i - 1].Size;
//            sortedLists[i - 1].Size = 0;
//        }

//        sortedLists[i - 1].Size = 0;
//        // Store the buckets in a new array which is sorted by bucket sizes
//        var outputBuckets = new Bucket[this.BucketCount];

//        for (i = 0; i < this.BucketCount; i++)
//        {
//            bucketSize = inputBuckets[i].Size;
//            if (bucketSize == 0)
//                continue;

//            position = sortedLists[bucketSize].BucketsList + sortedLists[bucketSize].Size;
//            outputBuckets[position].BucketId = i;
//            outputBuckets[position].ItemsList = inputBuckets[i].ItemsList;
//            sortedLists[bucketSize].Size++;
//        }

//        this.buckets = outputBuckets;

//        // Store the items according to the new order of buckets.
//        var outputItems = new Item[this.BinCount];
//        position = 0;

//        for (bucketSize = 1; bucketSize <= maxBucketSize; bucketSize++)
//            for (i = sortedLists[bucketSize].BucketsList;
//                 i < sortedLists[bucketSize].Size + sortedLists[bucketSize].BucketsList;
//                 i++)
//            {
//                var position2 = outputBuckets[i].ItemsList;
//                outputBuckets[i].ItemsList = position;
//                for (uint j = 0; j < bucketSize; j++)
//                {
//                    outputItems[position].F = inputItems[position2].F;
//                    outputItems[position].H = inputItems[position2].H;
//                    position++;
//                    position2++;
//                }
//            }

//        //Return the items sorted in new order and free the old items sorted in old order
//        this.items = outputItems;
//        return sortedLists;
//    }

//    private bool PlaceBucketProbe(uint probe0Num, uint probe1Num, uint bucketNum, uint size, BitArray occupTable)
//    {
//        uint i;
//        uint position;

//        var p = this.buckets[bucketNum].ItemsList;

//        // try place bucket with probe_num
//        for (i = 0; i < size; i++) // placement
//        {
//            position = (uint)((this.items[p].F + (ulong)this.items[p].H * probe0Num + probe1Num) % this.BinCount);
//            if (occupTable.GetBit(position))
//                break;
//            occupTable.SetBit(position);
//            p++;
//        }
//        if (i == size)
//            return true;
//        // Undo the placement
//        p = this.buckets[bucketNum].ItemsList;
//        for (; ; )
//        {
//            if (i == 0)
//                break;
//            position = (uint)((this.items[p].F + (ulong)this.items[p].H * probe0Num + probe1Num) % this.BinCount);
//            occupTable.UnSetBit(position);

//            // 				([position/32]^=(1<<(position%32));
//            p++;
//            i--;
//        }
//        return false;
//    }

//    public bool SearchingPhase(uint maxBucketSize, BucketSortedList[] sortedLists, uint[] dispTable)
//    {
//        var maxProbes = (uint)(Math.Log(this.keyCount) / Math.Log(2.0) / 20 * HashBuckets.maxProbesBase);
//        uint i;
//        var occupTable = new BitArray((int)((this.BinCount + 31) / 32 * sizeof(uint)));

//        for (i = maxBucketSize; i > 0; i--)
//        {
//            uint probeNum = 0;
//            uint probe0Num = 0;
//            uint probe1Num = 0;
//            var sortedListSize = sortedLists[i].Size;
//            while (sortedLists[i].Size != 0)
//            {
//                var currBucket = sortedLists[i].BucketsList;
//                uint nonPlacedBucket = 0;
//                for (uint j = 0; j < sortedLists[i].Size; j++)
//                {
//                    // if bucket is successfully placed remove it from list
//                    if (this.PlaceBucketProbe(probe0Num, probe1Num, currBucket, i, occupTable))
//                        dispTable[this.buckets[currBucket].BucketId] = probe0Num + probe1Num * this.BinCount;
//                    else
//                    {
//                        this.buckets[nonPlacedBucket + sortedLists[i].BucketsList].ItemsList =
//                            this.buckets[currBucket].ItemsList;
//                        this.buckets[nonPlacedBucket + sortedLists[i].BucketsList].BucketId =
//                            this.buckets[currBucket].BucketId;
//                        nonPlacedBucket++;
//                    }
//                    currBucket++;
//                }
//                sortedLists[i].Size = nonPlacedBucket;
//                probe0Num++;
//                if (probe0Num >= this.BinCount)
//                {
//                    probe0Num -= this.BinCount;
//                    probe1Num++;
//                }
//                probeNum++;
//                if (probeNum < maxProbes && probe1Num < this.BinCount)
//                    continue;
//                sortedLists[i].Size = sortedListSize;
//                return false;
//            }
//            sortedLists[i].Size = sortedListSize;
//        }
//        return true;
//    }
//}