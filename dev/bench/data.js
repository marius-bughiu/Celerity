window.BENCHMARK_DATA = {
  "lastUpdate": 1780603603339,
  "repoUrl": "https://github.com/marius-bughiu/Celerity",
  "entries": {
    "Celerity Benchmarks": [
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "5539154c7f0e48b9d1d4264ef8659f2c3746ccab",
          "message": "Merge pull request #96 from marius-bughiu/ci/benchmark-tracking\n\nci: continuous benchmark tracking vs .NET BCL",
          "timestamp": "2026-05-17T11:09:08+03:00",
          "tree_id": "22fdbc568ac8acf38a77eefe7eb9501909f7f8f3",
          "url": "https://github.com/marius-bughiu/Celerity/commit/5539154c7f0e48b9d1d4264ef8659f2c3746ccab"
        },
        "date": 1779005953441,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 11765.959887695313,
            "unit": "ns",
            "range": "± 59.19126248495176"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 11713.01145324707,
            "unit": "ns",
            "range": "± 70.10073165792816"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 10645.842700195313,
            "unit": "ns",
            "range": "± 32.17202128818875"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9631.641726684571,
            "unit": "ns",
            "range": "± 49.838696085236364"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4928451.12265625,
            "unit": "ns",
            "range": "± 40411.25239605994"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4940897.390625,
            "unit": "ns",
            "range": "± 64914.10318518835"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3315655.610546875,
            "unit": "ns",
            "range": "± 6493.257201044317"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3304653.641796875,
            "unit": "ns",
            "range": "± 28280.27983636996"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4713.383076477051,
            "unit": "ns",
            "range": "± 27.31544368857153"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4728.882112121582,
            "unit": "ns",
            "range": "± 16.059843752038546"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2210.1544662475585,
            "unit": "ns",
            "range": "± 12.282100120920045"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2203.525294303894,
            "unit": "ns",
            "range": "± 5.633259636908943"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1567986.6453125,
            "unit": "ns",
            "range": "± 6343.11521261854"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1570779.572265625,
            "unit": "ns",
            "range": "± 6240.74687276206"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 613659.441015625,
            "unit": "ns",
            "range": "± 2987.8444133042954"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 615088.876171875,
            "unit": "ns",
            "range": "± 2313.3617599400704"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15561.508828735352,
            "unit": "ns",
            "range": "± 306.8291149495189"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 12622.458615112304,
            "unit": "ns",
            "range": "± 59.02162563225654"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14014.608688354492,
            "unit": "ns",
            "range": "± 59.6414204071845"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12243.605728149414,
            "unit": "ns",
            "range": "± 22.367372568989826"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11503.890393066406,
            "unit": "ns",
            "range": "± 95.39080754683518"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 13660.807559967041,
            "unit": "ns",
            "range": "± 19.813045100553193"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4083252.73125,
            "unit": "ns",
            "range": "± 61416.458166439224"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4062513.0109375,
            "unit": "ns",
            "range": "± 44963.24421303844"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4982685.091796875,
            "unit": "ns",
            "range": "± 11137.255088011663"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4850932.7109375,
            "unit": "ns",
            "range": "± 21168.830120867737"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4926240.40625,
            "unit": "ns",
            "range": "± 9047.572246769647"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7540797.39453125,
            "unit": "ns",
            "range": "± 196438.04851972588"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4716.09761352539,
            "unit": "ns",
            "range": "± 9.797893229840346"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4714.774446105957,
            "unit": "ns",
            "range": "± 16.511388474383423"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4893.086944580078,
            "unit": "ns",
            "range": "± 12.550036881490964"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2709.415901184082,
            "unit": "ns",
            "range": "± 4.760714622724442"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2817.999069976807,
            "unit": "ns",
            "range": "± 52.784739352744644"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4016.9025650024414,
            "unit": "ns",
            "range": "± 18.36898022865456"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1593310.2602539062,
            "unit": "ns",
            "range": "± 1067.77521480461"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1591346.364453125,
            "unit": "ns",
            "range": "± 5558.631195238979"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1572688.0092773438,
            "unit": "ns",
            "range": "± 826.2511419481705"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 690181.5615234375,
            "unit": "ns",
            "range": "± 280.9053761946104"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 683906.5416015625,
            "unit": "ns",
            "range": "± 2940.641693720978"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 945382.67109375,
            "unit": "ns",
            "range": "± 1853.6341027178255"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2065.963082122803,
            "unit": "ns",
            "range": "± 4.408325144738091"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 2001.735887145996,
            "unit": "ns",
            "range": "± 9.115010756484375"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2066.000794219971,
            "unit": "ns",
            "range": "± 2.885403349629869"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 1995.9450159072876,
            "unit": "ns",
            "range": "± 8.92025607712159"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2182.7111305236817,
            "unit": "ns",
            "range": "± 3.9917153421280136"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 2808.7194244384764,
            "unit": "ns",
            "range": "± 0.6949513657588574"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 1558.5409858703613,
            "unit": "ns",
            "range": "± 5.7484302533731375"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 2794.9810653686523,
            "unit": "ns",
            "range": "± 12.759247597755323"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 1354.354637527466,
            "unit": "ns",
            "range": "± 2.7813572273888774"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 4099.641822814941,
            "unit": "ns",
            "range": "± 38.32708535274185"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 291286.8321289063,
            "unit": "ns",
            "range": "± 1359.0698280263832"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 287052.91865234374,
            "unit": "ns",
            "range": "± 1790.4470903792983"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 278277.137890625,
            "unit": "ns",
            "range": "± 2003.1992361820028"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 276260.771484375,
            "unit": "ns",
            "range": "± 1943.326488342115"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 312150.0755859375,
            "unit": "ns",
            "range": "± 1803.5043953159538"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 423430.50390625,
            "unit": "ns",
            "range": "± 343.67568970627195"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 277420.76044921874,
            "unit": "ns",
            "range": "± 3110.140858019709"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 387350.43994140625,
            "unit": "ns",
            "range": "± 298.10938319229837"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 252178.8427734375,
            "unit": "ns",
            "range": "± 1024.168297598219"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 608309.5495117188,
            "unit": "ns",
            "range": "± 1750.2716857110508"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "distinct": true,
          "id": "e96e00b7e894736e95d60d1c830dfa4449d10162",
          "message": "Release v1.2.1",
          "timestamp": "2026-05-17T11:10:59+03:00",
          "tree_id": "3fb38332649bfc6e0f2aecf75b6f6d6fd45a6bb4",
          "url": "https://github.com/marius-bughiu/Celerity/commit/e96e00b7e894736e95d60d1c830dfa4449d10162"
        },
        "date": 1779006059036,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12552.610137939453,
            "unit": "ns",
            "range": "± 51.16681096516174"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12657.101397705079,
            "unit": "ns",
            "range": "± 41.029623870012294"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 10645.049800872803,
            "unit": "ns",
            "range": "± 34.42974647536059"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10593.066467285156,
            "unit": "ns",
            "range": "± 43.009705300497714"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5085702.017578125,
            "unit": "ns",
            "range": "± 22068.03161408296"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5160009.216796875,
            "unit": "ns",
            "range": "± 106886.0705486995"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3683183.93203125,
            "unit": "ns",
            "range": "± 25705.72703164814"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3646965.29609375,
            "unit": "ns",
            "range": "± 13760.237772804396"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5334.994427490235,
            "unit": "ns",
            "range": "± 1.2782462226969893"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4713.312237548828,
            "unit": "ns",
            "range": "± 9.104531202528507"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2446.3240730285643,
            "unit": "ns",
            "range": "± 2.243438816245688"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2354.363621711731,
            "unit": "ns",
            "range": "± 2.1856400472103688"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1512295.407421875,
            "unit": "ns",
            "range": "± 2693.5918450165095"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1511821.4326171875,
            "unit": "ns",
            "range": "± 304.911775797023"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 590398.1484375,
            "unit": "ns",
            "range": "± 41465.22368535857"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 534915.5390625,
            "unit": "ns",
            "range": "± 5748.917205675774"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13148.918161010743,
            "unit": "ns",
            "range": "± 57.61424712264798"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13438.171977996826,
            "unit": "ns",
            "range": "± 49.80633519225624"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15106.604541015626,
            "unit": "ns",
            "range": "± 96.47885205991643"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9571.62594909668,
            "unit": "ns",
            "range": "± 178.47675015652501"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9542.051907348632,
            "unit": "ns",
            "range": "± 149.89810629933885"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 14760.63307800293,
            "unit": "ns",
            "range": "± 222.24435527070554"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4428172.0390625,
            "unit": "ns",
            "range": "± 125231.01876720643"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4343178.4,
            "unit": "ns",
            "range": "± 32682.58016476094"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5186001.7578125,
            "unit": "ns",
            "range": "± 30428.504983227194"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5342397.97265625,
            "unit": "ns",
            "range": "± 16837.840429944055"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5276540.2453125,
            "unit": "ns",
            "range": "± 26696.687492015633"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 8622723.9765625,
            "unit": "ns",
            "range": "± 48211.31416628316"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4834.437511444092,
            "unit": "ns",
            "range": "± 0.6474003047353853"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4602.043138504028,
            "unit": "ns",
            "range": "± 0.768500298656587"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5002.435865783691,
            "unit": "ns",
            "range": "± 28.555634611586648"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2641.4510078430176,
            "unit": "ns",
            "range": "± 0.45858343972920773"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2677.2151556015015,
            "unit": "ns",
            "range": "± 8.385231189497402"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4569.196670532227,
            "unit": "ns",
            "range": "± 1.6834965136348603"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1606621.2861328125,
            "unit": "ns",
            "range": "± 2197.1011619269407"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1541432.44609375,
            "unit": "ns",
            "range": "± 2076.7039267677496"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1590580.7182617188,
            "unit": "ns",
            "range": "± 1443.9370895899442"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 536718.3478515625,
            "unit": "ns",
            "range": "± 5764.570809176066"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 586614.9904785156,
            "unit": "ns",
            "range": "± 11085.578308749056"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 907078.2943359375,
            "unit": "ns",
            "range": "± 6622.073126494115"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2315.779646873474,
            "unit": "ns",
            "range": "± 1.4266868452395358"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 2264.3470602035522,
            "unit": "ns",
            "range": "± 2.6471121234393507"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2300.124683380127,
            "unit": "ns",
            "range": "± 2.288175133805476"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 2247.3989707946776,
            "unit": "ns",
            "range": "± 4.0410017644920755"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2475.5585765838623,
            "unit": "ns",
            "range": "± 1.8621221225333606"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 2822.939782142639,
            "unit": "ns",
            "range": "± 1.08328509402915"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 1514.5600004196167,
            "unit": "ns",
            "range": "± 0.9350030072253848"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 2827.7816047668457,
            "unit": "ns",
            "range": "± 1.0456642377125767"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 1501.3172702789307,
            "unit": "ns",
            "range": "± 1.447801393097295"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 4245.61999130249,
            "unit": "ns",
            "range": "± 1.2798526924129252"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 251825.628515625,
            "unit": "ns",
            "range": "± 241.44300680755885"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 236279.9038696289,
            "unit": "ns",
            "range": "± 134.36430230730372"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 253255.74755859375,
            "unit": "ns",
            "range": "± 150.65766129580763"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 240901.25952148438,
            "unit": "ns",
            "range": "± 861.4632006174414"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 270293.5555664062,
            "unit": "ns",
            "range": "± 468.0289880753558"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 347759.56494140625,
            "unit": "ns",
            "range": "± 504.2783117766013"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 246055.03173828125,
            "unit": "ns",
            "range": "± 163.1337815655377"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 336565.1887207031,
            "unit": "ns",
            "range": "± 1025.4419979105623"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 229003.195703125,
            "unit": "ns",
            "range": "± 171.58236867730142"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 549742.0326171875,
            "unit": "ns",
            "range": "± 2229.2810583122805"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d0173e9936aafb2aa914dd8653e0b8c0bc81299d",
          "message": "Merge pull request #97 from marius-bughiu/ci/benchmark-dashboard\n\nci: branded landing page + custom benchmark dashboard",
          "timestamp": "2026-05-17T11:45:10+03:00",
          "tree_id": "329cca60c33d585d89b1e89307c78b24e35bdd0d",
          "url": "https://github.com/marius-bughiu/Celerity/commit/d0173e9936aafb2aa914dd8653e0b8c0bc81299d"
        },
        "date": 1779008100425,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13115.031942749023,
            "unit": "ns",
            "range": "± 68.30561744166417"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12654.848699951172,
            "unit": "ns",
            "range": "± 128.0891966744655"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 11208.017687988282,
            "unit": "ns",
            "range": "± 38.521616791934164"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10282.234870910645,
            "unit": "ns",
            "range": "± 30.88505554167054"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5004942.11328125,
            "unit": "ns",
            "range": "± 41721.10067456481"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5319864.18203125,
            "unit": "ns",
            "range": "± 80325.7028486046"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3314974.939453125,
            "unit": "ns",
            "range": "± 14233.390324349639"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3333396.9736328125,
            "unit": "ns",
            "range": "± 5480.236851150319"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4756.875663757324,
            "unit": "ns",
            "range": "± 14.491709082707839"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4744.959217071533,
            "unit": "ns",
            "range": "± 3.533145078124704"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2224.254513549805,
            "unit": "ns",
            "range": "± 7.296672501072102"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2212.66268081665,
            "unit": "ns",
            "range": "± 6.594222055924076"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1608244.369140625,
            "unit": "ns",
            "range": "± 7104.097067746772"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1606973.072265625,
            "unit": "ns",
            "range": "± 5719.25663640638"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 628019.6342773438,
            "unit": "ns",
            "range": "± 454.4709149770549"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 631487.6887695312,
            "unit": "ns",
            "range": "± 588.6218002255858"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13839.508590698242,
            "unit": "ns",
            "range": "± 221.7409722813"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14029.78484802246,
            "unit": "ns",
            "range": "± 163.3630915160547"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16412.798254394533,
            "unit": "ns",
            "range": "± 126.87403810692513"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 13114.088270568847,
            "unit": "ns",
            "range": "± 185.1465303225313"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 12187.278205871582,
            "unit": "ns",
            "range": "± 123.14473041893088"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 14827.399589538574,
            "unit": "ns",
            "range": "± 46.8264475229891"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4288284.33203125,
            "unit": "ns",
            "range": "± 10034.89321974587"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4208453.41875,
            "unit": "ns",
            "range": "± 73110.96806173057"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5091091.0703125,
            "unit": "ns",
            "range": "± 53322.18771208904"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4996532.775390625,
            "unit": "ns",
            "range": "± 17438.004342028125"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4891072.96484375,
            "unit": "ns",
            "range": "± 9054.126895561054"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7516887.1953125,
            "unit": "ns",
            "range": "± 50570.823222557345"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4693.343003845215,
            "unit": "ns",
            "range": "± 8.321524343873747"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4724.170448303223,
            "unit": "ns",
            "range": "± 4.051696744679702"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4896.8229507446285,
            "unit": "ns",
            "range": "± 10.656609429280689"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2708.9821395874023,
            "unit": "ns",
            "range": "± 6.708266224819651"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2826.515714263916,
            "unit": "ns",
            "range": "± 70.90849425859983"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4011.8323928833006,
            "unit": "ns",
            "range": "± 2.714894252677641"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1626572.8920898438,
            "unit": "ns",
            "range": "± 2334.598956449736"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1623913.109375,
            "unit": "ns",
            "range": "± 4025.32825747245"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1619826.51171875,
            "unit": "ns",
            "range": "± 8797.463641292696"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 701568.314453125,
            "unit": "ns",
            "range": "± 615.7723087508606"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 705866.55625,
            "unit": "ns",
            "range": "± 11616.476652827905"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 976466.9404296875,
            "unit": "ns",
            "range": "± 864.9245373474702"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2069.7678365707397,
            "unit": "ns",
            "range": "± 0.36026131076697665"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 2008.8751022338868,
            "unit": "ns",
            "range": "± 1.904170525263688"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2067.832099914551,
            "unit": "ns",
            "range": "± 2.706422598224542"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 2012.3138875961304,
            "unit": "ns",
            "range": "± 1.057398154011103"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 2201.0399169921875,
            "unit": "ns",
            "range": "± 1.3217438055391784"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 2809.1864557266235,
            "unit": "ns",
            "range": "± 0.9536985662931241"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 1564.2296986579895,
            "unit": "ns",
            "range": "± 0.2588759532298267"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 2810.9003105163574,
            "unit": "ns",
            "range": "± 0.5641876370916298"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 1335.736092376709,
            "unit": "ns",
            "range": "± 3.8539060172349835"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 4061.8956161499023,
            "unit": "ns",
            "range": "± 3.647950502473143"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 301658.58251953125,
            "unit": "ns",
            "range": "± 357.54825578238"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 286948.8527832031,
            "unit": "ns",
            "range": "± 215.96211148324392"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 297548.02099609375,
            "unit": "ns",
            "range": "± 501.1185077982707"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 275006.4757080078,
            "unit": "ns",
            "range": "± 234.71184447406512"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 318905.3731445313,
            "unit": "ns",
            "range": "± 439.20169314433645"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 434444.4046875,
            "unit": "ns",
            "range": "± 463.8325148759627"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 287895.1667480469,
            "unit": "ns",
            "range": "± 686.8305256365653"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 421438.7127929687,
            "unit": "ns",
            "range": "± 826.0941568118141"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 261058.85234375,
            "unit": "ns",
            "range": "± 239.0139386184591"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 630338.9587402344,
            "unit": "ns",
            "range": "± 230.1584332145255"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "4305f901048679dd327c600b2d3df2af7fad8f89",
          "message": "Merge pull request #99 from marius-bughiu/ui/dashboard-detail-page\n\nui: ms y-axis, click-through detail page, README hero, wider landing",
          "timestamp": "2026-05-17T12:19:00+03:00",
          "tree_id": "d02e8216f8a62214fa901a8f03e34eda36b491d3",
          "url": "https://github.com/marius-bughiu/Celerity/commit/4305f901048679dd327c600b2d3df2af7fad8f89"
        },
        "date": 1779010016588,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12364.45811843872,
            "unit": "ns",
            "range": "± 8.217281684494463"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12493.0046875,
            "unit": "ns",
            "range": "± 19.823886131406567"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 10831.627703857423,
            "unit": "ns",
            "range": "± 191.60636676080054"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10486.644580841064,
            "unit": "ns",
            "range": "± 7.68253168277727"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5091585.7734375,
            "unit": "ns",
            "range": "± 48826.7233334445"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5292433.171875,
            "unit": "ns",
            "range": "± 74916.77521810851"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3641068.9521484375,
            "unit": "ns",
            "range": "± 4286.6462055899065"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3645911.79609375,
            "unit": "ns",
            "range": "± 17419.59713958251"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4712.851993560791,
            "unit": "ns",
            "range": "± 0.8243922211507315"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4699.368670654297,
            "unit": "ns",
            "range": "± 2.473749434477837"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2441.600396156311,
            "unit": "ns",
            "range": "± 2.4571417903393034"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2354.949980735779,
            "unit": "ns",
            "range": "± 0.6703377964692446"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1594917.266796875,
            "unit": "ns",
            "range": "± 12135.486457443209"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1519131.2270507812,
            "unit": "ns",
            "range": "± 1124.0134053447637"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 519852.1936035156,
            "unit": "ns",
            "range": "± 8318.964450009626"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 543256.5326171875,
            "unit": "ns",
            "range": "± 8261.964420212835"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13049.868427276611,
            "unit": "ns",
            "range": "± 20.39501204894377"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13130.365299224854,
            "unit": "ns",
            "range": "± 28.30296246564527"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14903.018811035156,
            "unit": "ns",
            "range": "± 42.572435735484206"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 10137.783352661132,
            "unit": "ns",
            "range": "± 165.47780465618047"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 10427.77951965332,
            "unit": "ns",
            "range": "± 144.05617937853626"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 14517.278729248046,
            "unit": "ns",
            "range": "± 27.320460362602436"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4504366.315625,
            "unit": "ns",
            "range": "± 150223.07649899923"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4332547.4859375,
            "unit": "ns",
            "range": "± 58270.33233291192"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5195460.201171875,
            "unit": "ns",
            "range": "± 3855.6554004041454"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5409917.590625,
            "unit": "ns",
            "range": "± 28004.039355080822"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5275085.240234375,
            "unit": "ns",
            "range": "± 48108.32059441583"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 8640704.453125,
            "unit": "ns",
            "range": "± 130923.76527808722"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4879.3468589782715,
            "unit": "ns",
            "range": "± 1.2536823394344763"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4604.222797393799,
            "unit": "ns",
            "range": "± 3.9179034244162865"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5026.929405212402,
            "unit": "ns",
            "range": "± 33.35993342409311"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2641.173968505859,
            "unit": "ns",
            "range": "± 0.7883759510252418"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2719.532861328125,
            "unit": "ns",
            "range": "± 10.926197645750458"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4569.823490142822,
            "unit": "ns",
            "range": "± 1.687335000924455"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1615024.828125,
            "unit": "ns",
            "range": "± 977.2914398894612"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1536504.1875,
            "unit": "ns",
            "range": "± 2042.3630736306484"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1595667.423828125,
            "unit": "ns",
            "range": "± 3260.2268659845754"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 618787.4923828125,
            "unit": "ns",
            "range": "± 39906.44155478429"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 640913.5695800781,
            "unit": "ns",
            "range": "± 843.0682336711399"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 901162.4197265625,
            "unit": "ns",
            "range": "± 11612.083718339718"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 53528.5,
            "unit": "ns",
            "range": "± 522.3051470803889"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 23800.25,
            "unit": "ns",
            "range": "± 1447.1252825285492"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 53422.75,
            "unit": "ns",
            "range": "± 303.55710610471084"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 17867,
            "unit": "ns",
            "range": "± 1961.253850644191"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 55345.75,
            "unit": "ns",
            "range": "± 219.40126860769666"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 154506,
            "unit": "ns",
            "range": "± 10839.93062708429"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 130862.5,
            "unit": "ns",
            "range": "± 648.9442708481728"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 61783.75,
            "unit": "ns",
            "range": "± 1493.0294426657053"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 61446.5,
            "unit": "ns",
            "range": "± 9532.178869492536"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 79577.75,
            "unit": "ns",
            "range": "± 3575.442478444684"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2010178.6,
            "unit": "ns",
            "range": "± 23078.152532211065"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1700382.3,
            "unit": "ns",
            "range": "± 16615.603019451326"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1967679.4,
            "unit": "ns",
            "range": "± 32649.258663865556"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1675659.6,
            "unit": "ns",
            "range": "± 22549.860760102267"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2017305.8,
            "unit": "ns",
            "range": "± 29300.760940630877"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 12117645,
            "unit": "ns",
            "range": "± 55462.91453454882"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 6325321.5,
            "unit": "ns",
            "range": "± 21298.82573054205"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 5231055.3,
            "unit": "ns",
            "range": "± 35563.53554133785"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1567908.3,
            "unit": "ns",
            "range": "± 31346.538344767832"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 7287750.9,
            "unit": "ns",
            "range": "± 215436.61679552062"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "db329a42ee21b83d25fe265d486bfddf57f1fab9",
          "message": "Merge pull request #100 from marius-bughiu/ci/nightly-preview-release\n\nci: add nightly preview release workflow",
          "timestamp": "2026-05-17T12:34:28+03:00",
          "tree_id": "cfd00927168b9ec834b27598abbbb2632e410f11",
          "url": "https://github.com/marius-bughiu/Celerity/commit/db329a42ee21b83d25fe265d486bfddf57f1fab9"
        },
        "date": 1779010974162,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12112.372018432618,
            "unit": "ns",
            "range": "± 176.809496227895"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 11964.361178588868,
            "unit": "ns",
            "range": "± 134.50833107281738"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 11008.095770263672,
            "unit": "ns",
            "range": "± 51.68090487943981"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10028.506042480469,
            "unit": "ns",
            "range": "± 91.77497943044102"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4997587.21171875,
            "unit": "ns",
            "range": "± 77586.7866768066"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4956631.125,
            "unit": "ns",
            "range": "± 76721.41078030002"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3373446.867578125,
            "unit": "ns",
            "range": "± 20668.978254219335"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3337504.719140625,
            "unit": "ns",
            "range": "± 12615.60743037612"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4731.932823181152,
            "unit": "ns",
            "range": "± 2.478988776094427"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4744.360382080078,
            "unit": "ns",
            "range": "± 2.3396220309969684"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2223.6343742370605,
            "unit": "ns",
            "range": "± 1.3839323039885278"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2212.755696296692,
            "unit": "ns",
            "range": "± 6.145999640763106"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1591377.038671875,
            "unit": "ns",
            "range": "± 2990.318156565179"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1578576.948046875,
            "unit": "ns",
            "range": "± 1106.3002901106372"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 619300.332421875,
            "unit": "ns",
            "range": "± 785.0602277823833"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 618828.5544433594,
            "unit": "ns",
            "range": "± 889.3182188788011"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13665.847760009765,
            "unit": "ns",
            "range": "± 332.833029772923"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14928.850059509277,
            "unit": "ns",
            "range": "± 436.62741288839527"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 17382.790795898436,
            "unit": "ns",
            "range": "± 3548.498627168167"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 13295.95506591797,
            "unit": "ns",
            "range": "± 87.10535160239493"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11972.514779663086,
            "unit": "ns",
            "range": "± 97.2751859147669"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 14305.672888183593,
            "unit": "ns",
            "range": "± 163.2499280635765"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4223599.778125,
            "unit": "ns",
            "range": "± 133722.89415387338"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4177039.1078125,
            "unit": "ns",
            "range": "± 82503.01619931527"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5009395.54296875,
            "unit": "ns",
            "range": "± 31949.662412010788"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5020294.7375,
            "unit": "ns",
            "range": "± 85912.7911369778"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4858667.409375,
            "unit": "ns",
            "range": "± 33830.893755791185"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7475068.5078125,
            "unit": "ns",
            "range": "± 38632.32076671695"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4718.376486206054,
            "unit": "ns",
            "range": "± 11.656286117341843"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4943.2878341674805,
            "unit": "ns",
            "range": "± 17.299786574527236"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4903.1046714782715,
            "unit": "ns",
            "range": "± 2.9911737493705193"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2709.4337196350098,
            "unit": "ns",
            "range": "± 6.508455323700581"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2839.2261032104493,
            "unit": "ns",
            "range": "± 53.389430613936675"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4014.871710205078,
            "unit": "ns",
            "range": "± 2.934333618778322"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1595721.5581054688,
            "unit": "ns",
            "range": "± 996.9269340948025"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1596760.725390625,
            "unit": "ns",
            "range": "± 1847.857582077081"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1585058.3603515625,
            "unit": "ns",
            "range": "± 780.9989192766307"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 692935.8803710938,
            "unit": "ns",
            "range": "± 903.1756570763018"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 686915.784765625,
            "unit": "ns",
            "range": "± 3920.8012430352305"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 1178120.7755859375,
            "unit": "ns",
            "range": "± 1119.8880621294684"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 63176.25,
            "unit": "ns",
            "range": "± 2490.1339154082993"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 21194,
            "unit": "ns",
            "range": "± 623.4757413083528"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 67599,
            "unit": "ns",
            "range": "± 9745.514737560043"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 20337.75,
            "unit": "ns",
            "range": "± 363.5027510212268"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 65391.25,
            "unit": "ns",
            "range": "± 820.9398577240601"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 230295.4,
            "unit": "ns",
            "range": "± 13551.340055507426"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 131625.75,
            "unit": "ns",
            "range": "± 2193.2618592711024"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 75514.8,
            "unit": "ns",
            "range": "± 9860.808141323914"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 60962,
            "unit": "ns",
            "range": "± 1271.542632657933"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 105655,
            "unit": "ns",
            "range": "± 1126.5395391788666"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2061736.25,
            "unit": "ns",
            "range": "± 13979.253446327764"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1743591.2,
            "unit": "ns",
            "range": "± 12346.784468030533"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2037792,
            "unit": "ns",
            "range": "± 6328.752760220611"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1707919.2,
            "unit": "ns",
            "range": "± 18712.221679960934"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2000360.3,
            "unit": "ns",
            "range": "± 28199.740108731497"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 12224088.2,
            "unit": "ns",
            "range": "± 110036.86047729642"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 6182968.5,
            "unit": "ns",
            "range": "± 54563.03110623529"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6210106.75,
            "unit": "ns",
            "range": "± 6561.989554751414"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1481765.3,
            "unit": "ns",
            "range": "± 15771.059434927001"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 9147889.8,
            "unit": "ns",
            "range": "± 180226.9098350743"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8293ef0b33867cd5ee78a74857747ed3236beb72",
          "message": "Merge pull request #101 from marius-bughiu/ci/benchmarks-workflow-overhaul\n\nci: split benchmarks workflow, custom PR comment, stable iterations",
          "timestamp": "2026-05-17T12:54:23+03:00",
          "tree_id": "2eb7011fd551b9a3f0da1e97a7e3713ed0465442",
          "url": "https://github.com/marius-bughiu/Celerity/commit/8293ef0b33867cd5ee78a74857747ed3236beb72"
        },
        "date": 1779013285455,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13210.881363178121,
            "unit": "ns",
            "range": "± 535.385430113909"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 16519.21790008545,
            "unit": "ns",
            "range": "± 3793.6471249764554"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 11440.06491983348,
            "unit": "ns",
            "range": "± 102.46181638445759"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10282.753341674805,
            "unit": "ns",
            "range": "± 62.86940363888505"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5040738.171527778,
            "unit": "ns",
            "range": "± 123188.7874566786"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5089234.522070313,
            "unit": "ns",
            "range": "± 113172.69550957636"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3374448.1178609915,
            "unit": "ns",
            "range": "± 27497.778614631316"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3364407.295052083,
            "unit": "ns",
            "range": "± 23509.35632863353"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4745.156760886864,
            "unit": "ns",
            "range": "± 5.657081206582112"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4751.814959773311,
            "unit": "ns",
            "range": "± 20.645591679115565"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2220.0531721443967,
            "unit": "ns",
            "range": "± 5.813445302318364"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2210.886073112488,
            "unit": "ns",
            "range": "± 6.616483610010911"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1594765.973563058,
            "unit": "ns",
            "range": "± 3459.927527463403"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1672580.4762257542,
            "unit": "ns",
            "range": "± 80453.61366729873"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 621777.3558175223,
            "unit": "ns",
            "range": "± 2063.7699066485184"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 621862.9378487723,
            "unit": "ns",
            "range": "± 1758.0863905659626"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15020.808384486607,
            "unit": "ns",
            "range": "± 973.1233105446415"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13913.574072265625,
            "unit": "ns",
            "range": "± 223.75314684350045"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15748.155091041743,
            "unit": "ns",
            "range": "± 416.72144630189314"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 13686.293450717269,
            "unit": "ns",
            "range": "± 672.2609898054358"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 12255.833266159583,
            "unit": "ns",
            "range": "± 135.62193009251243"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 15115.313681030273,
            "unit": "ns",
            "range": "± 145.13046988281613"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4189782.1744791665,
            "unit": "ns",
            "range": "± 50629.27935563416"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4236291.689012097,
            "unit": "ns",
            "range": "± 72690.69099525014"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5077198.995824354,
            "unit": "ns",
            "range": "± 70443.39212445318"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4978473.856026785,
            "unit": "ns",
            "range": "± 32297.243748670324"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4953666.172916667,
            "unit": "ns",
            "range": "± 44370.11739179916"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7575875.045833333,
            "unit": "ns",
            "range": "± 82608.40232613611"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4728.729231425694,
            "unit": "ns",
            "range": "± 8.828844381270304"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4717.112320963542,
            "unit": "ns",
            "range": "± 15.884891655875194"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4897.342536417643,
            "unit": "ns",
            "range": "± 30.684184300495797"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2702.76578780583,
            "unit": "ns",
            "range": "± 6.710551350752802"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2803.0717710256577,
            "unit": "ns",
            "range": "± 54.46307148941541"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4016.896704779731,
            "unit": "ns",
            "range": "± 7.002859641762339"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1606244.4259832974,
            "unit": "ns",
            "range": "± 4572.04687713324"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1602824.6068638393,
            "unit": "ns",
            "range": "± 6680.753741675421"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1601472.7209123883,
            "unit": "ns",
            "range": "± 7523.27813507821"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 695860.1232910156,
            "unit": "ns",
            "range": "± 1047.8953865544493"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 688240.4136646412,
            "unit": "ns",
            "range": "± 5043.445756183814"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 955090.3217424665,
            "unit": "ns",
            "range": "± 2685.147621203755"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80085.38481675393,
            "unit": "ns",
            "range": "± 7268.751367458248"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 31147.225130890052,
            "unit": "ns",
            "range": "± 4847.852101334956"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79740.5412371134,
            "unit": "ns",
            "range": "± 6730.285603256336"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 31089.861313868612,
            "unit": "ns",
            "range": "± 4657.431983309487"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82858.7027027027,
            "unit": "ns",
            "range": "± 6101.277702362953"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 178609.953125,
            "unit": "ns",
            "range": "± 29691.30630430337"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 158821.09523809524,
            "unit": "ns",
            "range": "± 15288.246463445428"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 87771.93975903615,
            "unit": "ns",
            "range": "± 6386.661713193621"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 76501.77295918367,
            "unit": "ns",
            "range": "± 6510.7717830537285"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 121143.59793814433,
            "unit": "ns",
            "range": "± 8355.954488425497"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2047934.5555555555,
            "unit": "ns",
            "range": "± 17386.0583897942"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1709269.8166666667,
            "unit": "ns",
            "range": "± 15637.703377482463"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2051477.7777777778,
            "unit": "ns",
            "range": "± 21637.09138410481"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1702555.4310344828,
            "unit": "ns",
            "range": "± 14870.90701415503"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2188941.043859649,
            "unit": "ns",
            "range": "± 356767.3678775585"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 2334916.8516483516,
            "unit": "ns",
            "range": "± 465260.8171048461"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1560525.4142857143,
            "unit": "ns",
            "range": "± 31310.08690842338"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2107335.2977099237,
            "unit": "ns",
            "range": "± 172208.4011966849"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1472221.7857142857,
            "unit": "ns",
            "range": "± 28000.97642260083"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 3147383.643258427,
            "unit": "ns",
            "range": "± 853213.2850094249"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "416a7c9b58773ffc001b5722535ec1f16b0ca25d",
          "message": "Merge pull request #102 from marius-bughiu/feat/long-set\n\nfeat: add LongSet for parity with LongDictionary",
          "timestamp": "2026-05-17T16:45:06+03:00",
          "tree_id": "f62a28cc79ba3d3a44f4e36f25eb0f44cf06bca7",
          "url": "https://github.com/marius-bughiu/Celerity/commit/416a7c9b58773ffc001b5722535ec1f16b0ca25d"
        },
        "date": 1779027561510,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13658.663853115506,
            "unit": "ns",
            "range": "± 41.07653960117575"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13350.540853227887,
            "unit": "ns",
            "range": "± 75.67227676694202"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13391.343492126465,
            "unit": "ns",
            "range": "± 103.4531450056559"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 11251.081087835904,
            "unit": "ns",
            "range": "± 158.29625828724764"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 10666.215452688712,
            "unit": "ns",
            "range": "± 71.97150097015518"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 15412.583518981934,
            "unit": "ns",
            "range": "± 34.11231942162139"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5300873.271223959,
            "unit": "ns",
            "range": "± 64251.42768633368"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5264781.958512931,
            "unit": "ns",
            "range": "± 76437.77436825604"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5084206.650152439,
            "unit": "ns",
            "range": "± 114703.67213719587"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3656494.147981771,
            "unit": "ns",
            "range": "± 36067.643891256295"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3617860.5515220906,
            "unit": "ns",
            "range": "± 19360.514325201762"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 7437583.778846154,
            "unit": "ns",
            "range": "± 23720.49267778279"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5100.632980933557,
            "unit": "ns",
            "range": "± 406.71265967208546"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4702.510415438948,
            "unit": "ns",
            "range": "± 4.645428005001996"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5071.958448850191,
            "unit": "ns",
            "range": "± 18.647568954292524"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 2436.371215255172,
            "unit": "ns",
            "range": "± 3.6465227005420093"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 2353.478758445153,
            "unit": "ns",
            "range": "± 0.74955179931365"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 4530.702436887301,
            "unit": "ns",
            "range": "± 1.8249457286677235"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1454682.824468085,
            "unit": "ns",
            "range": "± 47556.62343469605"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1531203.470775463,
            "unit": "ns",
            "range": "± 29531.806162555273"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1569555.7774188702,
            "unit": "ns",
            "range": "± 4019.099620968278"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 536443.1096622243,
            "unit": "ns",
            "range": "± 36268.40619944311"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 530944.2868088942,
            "unit": "ns",
            "range": "± 6133.7239788400475"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 850813.1536560059,
            "unit": "ns",
            "range": "± 15662.244624949817"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13974.852027045355,
            "unit": "ns",
            "range": "± 407.95729482989145"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13869.42498183832,
            "unit": "ns",
            "range": "± 336.70426010454463"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15818.557047350654,
            "unit": "ns",
            "range": "± 184.6161132262606"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 10320.723587562297,
            "unit": "ns",
            "range": "± 264.5020399331258"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 10272.390460713705,
            "unit": "ns",
            "range": "± 539.9539001884423"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 15097.442485809326,
            "unit": "ns",
            "range": "± 64.54016220430091"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4475555.772977941,
            "unit": "ns",
            "range": "± 80093.36607199401"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4455834.834872159,
            "unit": "ns",
            "range": "± 91616.5871324085"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5313616.729817708,
            "unit": "ns",
            "range": "± 45787.644428852036"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5406809.974348959,
            "unit": "ns",
            "range": "± 55362.06032225619"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5310764.073814655,
            "unit": "ns",
            "range": "± 51725.40163322648"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 8709614.8640625,
            "unit": "ns",
            "range": "± 114349.41585691656"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4833.8786988939555,
            "unit": "ns",
            "range": "± 7.427881849689359"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4598.778385021068,
            "unit": "ns",
            "range": "± 2.266561366539916"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5039.564310201009,
            "unit": "ns",
            "range": "± 41.874159899671504"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2897.170612188486,
            "unit": "ns",
            "range": "± 266.9667518166762"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2732.6958190654886,
            "unit": "ns",
            "range": "± 20.489556209399147"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 4568.871962370696,
            "unit": "ns",
            "range": "± 8.369666084767077"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1609399.369075521,
            "unit": "ns",
            "range": "± 8584.542320677978"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1536606.9787248883,
            "unit": "ns",
            "range": "± 3231.2871800248117"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1592852.1529947917,
            "unit": "ns",
            "range": "± 5082.868507752879"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 593351.6366299716,
            "unit": "ns",
            "range": "± 56422.20902547143"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 607919.5816685267,
            "unit": "ns",
            "range": "± 21978.254843818686"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 893905.1376255581,
            "unit": "ns",
            "range": "± 6106.040471076745"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79414.1861111111,
            "unit": "ns",
            "range": "± 4997.663423882143"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27380.786096256685,
            "unit": "ns",
            "range": "± 2523.527279918689"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79509.55154639175,
            "unit": "ns",
            "range": "± 8481.578230172216"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30704.157692307694,
            "unit": "ns",
            "range": "± 3580.756566604529"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81961.68644067796,
            "unit": "ns",
            "range": "± 6680.709865209725"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 71809.55263157895,
            "unit": "ns",
            "range": "± 6932.6472046694425"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 164376.86785714285,
            "unit": "ns",
            "range": "± 9492.057386776141"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 156774.83333333334,
            "unit": "ns",
            "range": "± 8076.24303264755"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 83661.69230769231,
            "unit": "ns",
            "range": "± 6070.10581645564"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 77972.73873873874,
            "unit": "ns",
            "range": "± 6495.655535484274"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 98961.74747474748,
            "unit": "ns",
            "range": "± 10240.38968647342"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 93752.72797927461,
            "unit": "ns",
            "range": "± 8308.686421723083"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2010471.5862068965,
            "unit": "ns",
            "range": "± 31001.49430098635"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1676772.6379310344,
            "unit": "ns",
            "range": "± 18845.88501404056"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2055156.53,
            "unit": "ns",
            "range": "± 69756.13579561899"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1713063.0333333334,
            "unit": "ns",
            "range": "± 23011.76086487731"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2030343.1342105262,
            "unit": "ns",
            "range": "± 115822.96341577431"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1929825.7241379311,
            "unit": "ns",
            "range": "± 19567.35495145377"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 2137534.320754717,
            "unit": "ns",
            "range": "± 74842.94489045648"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1634865.8170731708,
            "unit": "ns",
            "range": "± 53541.48478917154"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 5286493.461538462,
            "unit": "ns",
            "range": "± 49400.54192433988"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1581141.15625,
            "unit": "ns",
            "range": "± 38689.05930422406"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 3095795.9943502825,
            "unit": "ns",
            "range": "± 419269.26994822145"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 2182850,
            "unit": "ns",
            "range": "± 17146.418384857272"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6a2a8559ac27311a90a7a23a56ebb3f82089dbf4",
          "message": "Merge pull request #103 from marius-bughiu/perf/remove-backward-shift-deletion\n\nperf(collections): backward-shift deletion replaces RehashAfterRemove",
          "timestamp": "2026-05-18T00:15:18+03:00",
          "tree_id": "2032a73a43462300ad2bee9684f1abd042534fe3",
          "url": "https://github.com/marius-bughiu/Celerity/commit/6a2a8559ac27311a90a7a23a56ebb3f82089dbf4"
        },
        "date": 1779054425312,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12740.077557881674,
            "unit": "ns",
            "range": "± 218.0807782721617"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12619.318416200835,
            "unit": "ns",
            "range": "± 106.91712104438476"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13420.482002521383,
            "unit": "ns",
            "range": "± 139.70799057086296"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9612.055056485262,
            "unit": "ns",
            "range": "± 208.083884044412"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9421.809146749561,
            "unit": "ns",
            "range": "± 259.2661994873487"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9878.002242533366,
            "unit": "ns",
            "range": "± 138.15722708029958"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5262166.042724609,
            "unit": "ns",
            "range": "± 115242.23583072903"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5199967.946928879,
            "unit": "ns",
            "range": "± 75601.7925450311"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5132436.215576172,
            "unit": "ns",
            "range": "± 96364.42394448062"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3515863.01796875,
            "unit": "ns",
            "range": "± 27761.56782276556"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3505166.4436961208,
            "unit": "ns",
            "range": "± 16359.491209423322"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6508450.56780134,
            "unit": "ns",
            "range": "± 94396.76682216772"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4702.808264051165,
            "unit": "ns",
            "range": "± 3.76990361417132"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4960.855158205385,
            "unit": "ns",
            "range": "± 274.73072850686407"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5054.542452494304,
            "unit": "ns",
            "range": "± 7.302268287168437"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1925.232556902129,
            "unit": "ns",
            "range": "± 16.27646302347301"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1918.6924304304453,
            "unit": "ns",
            "range": "± 20.081392472997354"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2090.752514566694,
            "unit": "ns",
            "range": "± 4.780330613637402"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1543006.21671875,
            "unit": "ns",
            "range": "± 26666.709502289585"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1512369.442533053,
            "unit": "ns",
            "range": "± 1873.396881153387"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1574538.48765346,
            "unit": "ns",
            "range": "± 1870.299778916769"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 495391.48779296875,
            "unit": "ns",
            "range": "± 11228.343068353906"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 503527.3740826231,
            "unit": "ns",
            "range": "± 9309.284609199942"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 581233.8763122559,
            "unit": "ns",
            "range": "± 11431.885029343768"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13354.085987797489,
            "unit": "ns",
            "range": "± 260.57049188399685"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13420.630705208614,
            "unit": "ns",
            "range": "± 234.3610072462251"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14838.049534606933,
            "unit": "ns",
            "range": "± 114.9291169701764"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9181.614565168109,
            "unit": "ns",
            "range": "± 210.45192253762625"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9005.854703903198,
            "unit": "ns",
            "range": "± 230.3561673211017"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9394.549095153809,
            "unit": "ns",
            "range": "± 159.85998821040948"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4372940.498263889,
            "unit": "ns",
            "range": "± 93024.9267593058"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4326153.426724138,
            "unit": "ns",
            "range": "± 55798.238801674444"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5245119.092592592,
            "unit": "ns",
            "range": "± 41074.335490328725"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5250322.627963362,
            "unit": "ns",
            "range": "± 79288.78178305384"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5204877.727994791,
            "unit": "ns",
            "range": "± 47034.39836191907"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7662453.35228588,
            "unit": "ns",
            "range": "± 72536.91961621516"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4718.304139313875,
            "unit": "ns",
            "range": "± 7.423659038358239"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4600.680419639305,
            "unit": "ns",
            "range": "± 4.1997240484759315"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5315.11270741054,
            "unit": "ns",
            "range": "± 301.30146618673285"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2144.215908839785,
            "unit": "ns",
            "range": "± 26.227885627823103"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2032.490025838216,
            "unit": "ns",
            "range": "± 19.04278386473597"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2230.447672350653,
            "unit": "ns",
            "range": "± 15.018211742489306"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1609405.818080357,
            "unit": "ns",
            "range": "± 7639.506558488818"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1533866.0799278845,
            "unit": "ns",
            "range": "± 2429.6231891100683"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1591204.536820023,
            "unit": "ns",
            "range": "± 5797.050830245323"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 582854.5302297109,
            "unit": "ns",
            "range": "± 24304.486985794934"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 637086.2998535156,
            "unit": "ns",
            "range": "± 15015.828489873915"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 671043.503452846,
            "unit": "ns",
            "range": "± 5376.396994445947"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 78926.65445026178,
            "unit": "ns",
            "range": "± 7418.704265923403"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26177.3125,
            "unit": "ns",
            "range": "± 2185.4861784938003"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 77721.6484375,
            "unit": "ns",
            "range": "± 7793.388720666457"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25883.476683937824,
            "unit": "ns",
            "range": "± 1957.3040080483875"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83784.77720207254,
            "unit": "ns",
            "range": "± 7904.716712132122"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70697.93854748603,
            "unit": "ns",
            "range": "± 4818.282970969764"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 147233.5267857143,
            "unit": "ns",
            "range": "± 6737.065813731921"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 137735.69166666668,
            "unit": "ns",
            "range": "± 7859.158350062592"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 88699.42268041238,
            "unit": "ns",
            "range": "± 7952.568212717734"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 85612.21683673469,
            "unit": "ns",
            "range": "± 7062.376810828301"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 81552.94897959183,
            "unit": "ns",
            "range": "± 7093.09400700818"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 74821.42146596858,
            "unit": "ns",
            "range": "± 5851.749898342148"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2003230.6333333333,
            "unit": "ns",
            "range": "± 29036.755679576225"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1675618.65,
            "unit": "ns",
            "range": "± 20295.940812163946"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2001188.6585365853,
            "unit": "ns",
            "range": "± 44443.18869276246"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1675767.607142857,
            "unit": "ns",
            "range": "± 14366.959763288374"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1996162.15625,
            "unit": "ns",
            "range": "± 36676.08429437836"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1944817.9107142857,
            "unit": "ns",
            "range": "± 21711.513123077915"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1703943.7413793104,
            "unit": "ns",
            "range": "± 21265.730453099222"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1425128.5298507463,
            "unit": "ns",
            "range": "± 56460.61011166027"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6156752.366666666,
            "unit": "ns",
            "range": "± 36729.37246451104"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1431678.8,
            "unit": "ns",
            "range": "± 14066.300097800391"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1829964.217557252,
            "unit": "ns",
            "range": "± 109670.60184797265"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1555071.6896551724,
            "unit": "ns",
            "range": "± 20348.777271275063"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8fbbd2f9d70d48910c6639d18ede6ecf1138addf",
          "message": "Merge pull request #105 from marius-bughiu/perf/resize-unsafe-bce\n\nperf(collections): Unsafe.Add bounds-check elimination on Resize",
          "timestamp": "2026-05-18T21:11:39+03:00",
          "tree_id": "0dbc460ae8cc3bee39f5bd7f8543f48b1eb3fe23",
          "url": "https://github.com/marius-bughiu/Celerity/commit/8fbbd2f9d70d48910c6639d18ede6ecf1138addf"
        },
        "date": 1779130143466,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 9628.3055295591,
            "unit": "ns",
            "range": "± 34.7453133819671"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 9622.051746826171,
            "unit": "ns",
            "range": "± 75.34838700908197"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 10274.21717360924,
            "unit": "ns",
            "range": "± 57.06167753159428"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 7199.091389247349,
            "unit": "ns",
            "range": "± 64.13541439789269"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 7024.077437400818,
            "unit": "ns",
            "range": "± 132.1777098021116"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 7392.57813526022,
            "unit": "ns",
            "range": "± 52.195540579697365"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4236339.228819445,
            "unit": "ns",
            "range": "± 135297.5988889702"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4199165.518920898,
            "unit": "ns",
            "range": "± 80155.49118339567"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4100298.9215448946,
            "unit": "ns",
            "range": "± 155569.7984651029"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 2729713.5926339286,
            "unit": "ns",
            "range": "± 31341.314260756095"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 2744451.663802083,
            "unit": "ns",
            "range": "± 19057.00392224055"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5398571.175520834,
            "unit": "ns",
            "range": "± 53740.4308034591"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3652.303649168748,
            "unit": "ns",
            "range": "± 3.4771700237568037"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3662.610837642963,
            "unit": "ns",
            "range": "± 10.24294302624072"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3957.1560261066143,
            "unit": "ns",
            "range": "± 4.632093590104076"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1490.5497221628825,
            "unit": "ns",
            "range": "± 16.81972630276321"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1481.1592383702596,
            "unit": "ns",
            "range": "± 15.966065038120043"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1624.2734721047539,
            "unit": "ns",
            "range": "± 2.371878989893655"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1172441.1767216434,
            "unit": "ns",
            "range": "± 1483.4194547768554"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1131298.2154224536,
            "unit": "ns",
            "range": "± 41450.36133153493"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1217194.4275251117,
            "unit": "ns",
            "range": "± 677.3315333610598"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 381542.8468940549,
            "unit": "ns",
            "range": "± 9132.177186223566"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 374070.5246803977,
            "unit": "ns",
            "range": "± 7119.285265146651"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 460250.95744536194,
            "unit": "ns",
            "range": "± 19617.442586285397"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 10886.9751824273,
            "unit": "ns",
            "range": "± 222.5247441550379"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 10516.388018290201,
            "unit": "ns",
            "range": "± 67.99150375313647"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 11408.055961608887,
            "unit": "ns",
            "range": "± 49.238819980843594"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 6851.04522819519,
            "unit": "ns",
            "range": "± 172.95436919264762"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 6564.674344282884,
            "unit": "ns",
            "range": "± 175.7432063709173"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 6427.913265934697,
            "unit": "ns",
            "range": "± 73.2947775202258"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3412938.876813616,
            "unit": "ns",
            "range": "± 62375.56605656611"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3406145.5066964286,
            "unit": "ns",
            "range": "± 28270.231653272505"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4183391.6296875,
            "unit": "ns",
            "range": "± 34093.56071819886"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4131297.3450969825,
            "unit": "ns",
            "range": "± 42120.96598545732"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4079969.1931573274,
            "unit": "ns",
            "range": "± 76153.70828486176"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6106106.698177083,
            "unit": "ns",
            "range": "± 37793.19743931964"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3654.1617553145798,
            "unit": "ns",
            "range": "± 93.10894291575902"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3570.1282652493182,
            "unit": "ns",
            "range": "± 1.8478293377863273"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3902.6626073903053,
            "unit": "ns",
            "range": "± 36.70486762249564"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 1648.0654351370674,
            "unit": "ns",
            "range": "± 35.433781815356156"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 1577.4103423809183,
            "unit": "ns",
            "range": "± 13.933250898867467"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 1741.843968356097,
            "unit": "ns",
            "range": "± 4.779702614662746"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1244585.2260237068,
            "unit": "ns",
            "range": "± 8429.688632676647"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1184182.7608506945,
            "unit": "ns",
            "range": "± 58315.238987394834"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1250660.1492745536,
            "unit": "ns",
            "range": "± 10816.717873707412"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 499132.9248285061,
            "unit": "ns",
            "range": "± 10903.169035010547"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 478432.82772460935,
            "unit": "ns",
            "range": "± 50202.6822854359"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 527093.8659645974,
            "unit": "ns",
            "range": "± 29242.312899221997"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 59222.846354166664,
            "unit": "ns",
            "range": "± 4714.611197050973"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 22398.177272727273,
            "unit": "ns",
            "range": "± 3602.5887809612814"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 59219.757894736846,
            "unit": "ns",
            "range": "± 5415.876260213359"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 22693.115979381444,
            "unit": "ns",
            "range": "± 3593.699297828748"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 62306.97382198953,
            "unit": "ns",
            "range": "± 4880.503266148898"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 58337.24397590361,
            "unit": "ns",
            "range": "± 4236.866751016909"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 115677.025,
            "unit": "ns",
            "range": "± 2625.017391761678"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 108663.5064516129,
            "unit": "ns",
            "range": "± 5558.367988426395"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 70128.95341614907,
            "unit": "ns",
            "range": "± 4783.248181087709"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 63134.163265306124,
            "unit": "ns",
            "range": "± 6667.717879983668"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 61660.60732984293,
            "unit": "ns",
            "range": "± 4252.128373284767"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 57693.180628272254,
            "unit": "ns",
            "range": "± 4138.413564283277"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1559722.537037037,
            "unit": "ns",
            "range": "± 18227.426090987767"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1302891.75,
            "unit": "ns",
            "range": "± 9512.20377285828"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1551117.6333333333,
            "unit": "ns",
            "range": "± 15166.1911352381"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1298629.1166666667,
            "unit": "ns",
            "range": "± 12094.454507722447"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1569940.709090909,
            "unit": "ns",
            "range": "± 52667.55527614375"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1501041.5333333334,
            "unit": "ns",
            "range": "± 15965.381593619524"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 3268339.8703703703,
            "unit": "ns",
            "range": "± 3325230.636828402"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1091196.6323529412,
            "unit": "ns",
            "range": "± 43989.30423583785"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2078237.5357142857,
            "unit": "ns",
            "range": "± 1447125.3643432274"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1116441.3,
            "unit": "ns",
            "range": "± 12502.82924988277"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1409675.8315789474,
            "unit": "ns",
            "range": "± 42592.73802112546"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1226801.9090909092,
            "unit": "ns",
            "range": "± 20598.51891878946"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "af902e3d3080d90298d51da84e9ceb7295caedf7",
          "message": "Merge pull request #111 from marius-bughiu/feat/string-murmur3-hasher\n\nfeat(hashing): add StringMurmur3Hasher for full-UTF-16 string hashing",
          "timestamp": "2026-05-22T18:28:21+03:00",
          "tree_id": "9049dfa430d69a96b203c9d7b00fb08c21c24e19",
          "url": "https://github.com/marius-bughiu/Celerity/commit/af902e3d3080d90298d51da84e9ceb7295caedf7"
        },
        "date": 1779465682252,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12688.315074375698,
            "unit": "ns",
            "range": "± 316.66075396328694"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12011.54951586042,
            "unit": "ns",
            "range": "± 106.91696887228267"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12964.023506709507,
            "unit": "ns",
            "range": "± 188.88447107188608"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8797.172689165387,
            "unit": "ns",
            "range": "± 125.1641204278709"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8534.640728541783,
            "unit": "ns",
            "range": "± 86.14616230588905"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 8938.499579594052,
            "unit": "ns",
            "range": "± 171.2751030469713"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5064322.455729167,
            "unit": "ns",
            "range": "± 104466.00657177027"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5033771.897673233,
            "unit": "ns",
            "range": "± 126724.37473912761"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4613763.011168574,
            "unit": "ns",
            "range": "± 226180.24810371656"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3148109.575125558,
            "unit": "ns",
            "range": "± 20522.87153176388"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3161695.8839285714,
            "unit": "ns",
            "range": "± 18271.77949967841"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6123591.847386854,
            "unit": "ns",
            "range": "± 67133.99373591001"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4749.616570366754,
            "unit": "ns",
            "range": "± 21.30091325396413"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4734.850928379939,
            "unit": "ns",
            "range": "± 3.4902081337622515"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4746.890942793626,
            "unit": "ns",
            "range": "± 8.78271276103335"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1857.9417026383537,
            "unit": "ns",
            "range": "± 8.810549248312102"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1801.1993533167347,
            "unit": "ns",
            "range": "± 8.05920709372405"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2003.451844215393,
            "unit": "ns",
            "range": "± 6.9208364647193665"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1570637.7569444445,
            "unit": "ns",
            "range": "± 24123.230460875693"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1549113.420828683,
            "unit": "ns",
            "range": "± 5159.126829096288"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1594947.1258370536,
            "unit": "ns",
            "range": "± 48069.334800136174"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 588150.7308391703,
            "unit": "ns",
            "range": "± 1479.0133520467425"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 569464.0290075232,
            "unit": "ns",
            "range": "± 1644.2726405925455"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 647260.845703125,
            "unit": "ns",
            "range": "± 2940.391608115291"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15232.236291738656,
            "unit": "ns",
            "range": "± 1533.825450590045"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13144.475780253508,
            "unit": "ns",
            "range": "± 388.52116286438167"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14869.838452148437,
            "unit": "ns",
            "range": "± 412.3198626431503"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11716.307434558868,
            "unit": "ns",
            "range": "± 257.43665814834793"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11445.555414134058,
            "unit": "ns",
            "range": "± 20.017222823815572"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11868.54546706741,
            "unit": "ns",
            "range": "± 299.9238379409719"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4147562.501885776,
            "unit": "ns",
            "range": "± 70254.58539928049"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4117291.98828125,
            "unit": "ns",
            "range": "± 73563.01850071852"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5060339.7922952585,
            "unit": "ns",
            "range": "± 64141.68797240347"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4782624.418229166,
            "unit": "ns",
            "range": "± 48751.54232206043"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4802521.4081357755,
            "unit": "ns",
            "range": "± 50634.10871371382"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6736299.069773707,
            "unit": "ns",
            "range": "± 76959.27148538102"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4836.053362353095,
            "unit": "ns",
            "range": "± 115.42626225938075"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4728.858935219901,
            "unit": "ns",
            "range": "± 9.944442195972117"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4838.586732369882,
            "unit": "ns",
            "range": "± 28.044269034286092"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2208.7017456054687,
            "unit": "ns",
            "range": "± 21.913429198794223"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2146.5858132289004,
            "unit": "ns",
            "range": "± 10.19982113913563"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2108.0734963989257,
            "unit": "ns",
            "range": "± 3.9797059129831833"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1604023.8797019676,
            "unit": "ns",
            "range": "± 8707.673601367978"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1605963.9652478448,
            "unit": "ns",
            "range": "± 5543.25160012036"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1585002.6280048077,
            "unit": "ns",
            "range": "± 7094.821669302297"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 653040.1768588362,
            "unit": "ns",
            "range": "± 1583.1092646039288"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 627938.8174501617,
            "unit": "ns",
            "range": "± 2291.5438259905104"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 670177.1668836805,
            "unit": "ns",
            "range": "± 1208.6306581937447"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80589.74863387978,
            "unit": "ns",
            "range": "± 6148.353089786538"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29728.59663865546,
            "unit": "ns",
            "range": "± 3582.977519287195"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82085.87046632124,
            "unit": "ns",
            "range": "± 8943.442439902094"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26257.16153846154,
            "unit": "ns",
            "range": "± 1294.704077433004"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 85023.89617486339,
            "unit": "ns",
            "range": "± 7408.36475224782"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 76284.15053763441,
            "unit": "ns",
            "range": "± 6096.355957980563"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 144207.84589041097,
            "unit": "ns",
            "range": "± 10738.866938652629"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 138105.40650406503,
            "unit": "ns",
            "range": "± 11832.149156829688"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 90119.5503875969,
            "unit": "ns",
            "range": "± 7294.776393035935"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 85574.48888888888,
            "unit": "ns",
            "range": "± 7403.298747015562"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 87856.89090909091,
            "unit": "ns",
            "range": "± 4444.518296404298"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 100202.4480874317,
            "unit": "ns",
            "range": "± 23654.22574394198"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2038860.6724137932,
            "unit": "ns",
            "range": "± 12543.475785155535"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1706051.4074074074,
            "unit": "ns",
            "range": "± 11939.12322192901"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2031957.5714285714,
            "unit": "ns",
            "range": "± 13459.426377157251"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1718213.857142857,
            "unit": "ns",
            "range": "± 11102.120184448873"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2011801,
            "unit": "ns",
            "range": "± 21509.444058452642"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1981746.55,
            "unit": "ns",
            "range": "± 22472.80889681864"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1555697.125,
            "unit": "ns",
            "range": "± 14614.77476619362"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1271637.7739726028,
            "unit": "ns",
            "range": "± 21604.10868089954"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2719906.3515625,
            "unit": "ns",
            "range": "± 2085493.1444996996"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1328287.8214285714,
            "unit": "ns",
            "range": "± 12584.64273114795"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1651064.7467532468,
            "unit": "ns",
            "range": "± 106059.81211055223"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1401691,
            "unit": "ns",
            "range": "± 39019.76641640075"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0adc7d064282bd9378cadab484e5e25cb070e3e2",
          "message": "Merge pull request #112 from marius-bughiu/ci/benchmarks-same-runner-ab\n\nci: same-runner A/B benchmark comparison (fix false regressions)",
          "timestamp": "2026-05-22T20:52:28+03:00",
          "tree_id": "dc6643bf9fccf1f96104cb8607d599df83466033",
          "url": "https://github.com/marius-bughiu/Celerity/commit/0adc7d064282bd9378cadab484e5e25cb070e3e2"
        },
        "date": 1779474242478,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12101.116755167643,
            "unit": "ns",
            "range": "± 68.99529172109243"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12224.887235005697,
            "unit": "ns",
            "range": "± 87.36143838651127"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12707.370653505679,
            "unit": "ns",
            "range": "± 85.99220541729683"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8650.136767251151,
            "unit": "ns",
            "range": "± 53.05760732200582"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8473.617345174154,
            "unit": "ns",
            "range": "± 27.74470382491243"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 8934.245238205482,
            "unit": "ns",
            "range": "± 208.6506785476771"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4969852.085379465,
            "unit": "ns",
            "range": "± 126483.80545505838"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4965645.39740954,
            "unit": "ns",
            "range": "± 105427.33556912636"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4476408.690557065,
            "unit": "ns",
            "range": "± 215308.726964428"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3155681.350080819,
            "unit": "ns",
            "range": "± 22941.697480398918"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3147777.2001616377,
            "unit": "ns",
            "range": "± 21331.00150900279"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5987607.177262931,
            "unit": "ns",
            "range": "± 46137.12655857547"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4751.483925713434,
            "unit": "ns",
            "range": "± 26.767408414719178"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4730.872998204724,
            "unit": "ns",
            "range": "± 14.194073400742717"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4730.877184186663,
            "unit": "ns",
            "range": "± 22.535617023570463"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1859.1171902247838,
            "unit": "ns",
            "range": "± 8.873665699671097"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1794.0444649005758,
            "unit": "ns",
            "range": "± 9.605589030710956"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1996.7259926631532,
            "unit": "ns",
            "range": "± 12.931243664787043"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1558650.0266276042,
            "unit": "ns",
            "range": "± 11891.987165034494"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1588732.0543294272,
            "unit": "ns",
            "range": "± 24822.448299822983"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1611200.284765625,
            "unit": "ns",
            "range": "± 20854.1770043068"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 587685.1178104795,
            "unit": "ns",
            "range": "± 1966.3838789054446"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 568846.3542227909,
            "unit": "ns",
            "range": "± 2192.340208225725"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 633153.6521158854,
            "unit": "ns",
            "range": "± 4173.501104215075"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 12492.094386291505,
            "unit": "ns",
            "range": "± 94.29447548663558"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13461.032497660319,
            "unit": "ns",
            "range": "± 728.2535639961112"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14754.547223944413,
            "unit": "ns",
            "range": "± 396.7326917145938"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11321.236677381727,
            "unit": "ns",
            "range": "± 117.36950806676509"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11434.6173048753,
            "unit": "ns",
            "range": "± 26.727483937036766"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11158.540871683757,
            "unit": "ns",
            "range": "± 515.8173276412144"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4078139.5295410156,
            "unit": "ns",
            "range": "± 75918.97505839856"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4083039.800911458,
            "unit": "ns",
            "range": "± 70730.38087251666"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4949787.658405173,
            "unit": "ns",
            "range": "± 38747.56455367366"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4749654.020543981,
            "unit": "ns",
            "range": "± 31908.43882524511"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4684755.292018581,
            "unit": "ns",
            "range": "± 123513.66009005184"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6688398.208258929,
            "unit": "ns",
            "range": "± 120846.75216286305"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4722.779388155256,
            "unit": "ns",
            "range": "± 10.827402359116736"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4741.2201803799335,
            "unit": "ns",
            "range": "± 25.788480652704195"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4811.136983998616,
            "unit": "ns",
            "range": "± 44.901283952923194"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2196.5586225575416,
            "unit": "ns",
            "range": "± 20.18595121395828"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2135.272089821952,
            "unit": "ns",
            "range": "± 2.8498592864471823"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2106.520362309047,
            "unit": "ns",
            "range": "± 3.8703612155357616"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1596874.1509114583,
            "unit": "ns",
            "range": "± 5129.878358948969"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1611618.640552662,
            "unit": "ns",
            "range": "± 9872.697074408634"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1584649.6409617458,
            "unit": "ns",
            "range": "± 5957.814234369607"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 651750.4068885216,
            "unit": "ns",
            "range": "± 807.4056450620693"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 621455.1142202524,
            "unit": "ns",
            "range": "± 2209.3449568349192"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 663306.6921807651,
            "unit": "ns",
            "range": "± 5897.995570314745"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81079.33510638298,
            "unit": "ns",
            "range": "± 5475.622838056261"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26197.70588235294,
            "unit": "ns",
            "range": "± 1411.3984573629882"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 78704.35326086957,
            "unit": "ns",
            "range": "± 6212.661499495342"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30802.766025641027,
            "unit": "ns",
            "range": "± 4213.1846507546325"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83466.73796791444,
            "unit": "ns",
            "range": "± 5941.957898950946"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 75387.5,
            "unit": "ns",
            "range": "± 5499.764375542509"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 142343.14655172414,
            "unit": "ns",
            "range": "± 12497.939444254356"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 132374.76923076922,
            "unit": "ns",
            "range": "± 5771.901041629666"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 88038.19791666667,
            "unit": "ns",
            "range": "± 7024.474283948905"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83713.36842105263,
            "unit": "ns",
            "range": "± 6515.405675996471"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 87196.77611940299,
            "unit": "ns",
            "range": "± 5077.075085438941"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 86369.66055045872,
            "unit": "ns",
            "range": "± 5074.287500103546"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2038671.8275862068,
            "unit": "ns",
            "range": "± 10887.506313165188"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1702042.4117647058,
            "unit": "ns",
            "range": "± 14480.941044085555"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2035934.9137931035,
            "unit": "ns",
            "range": "± 11827.399579146844"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1713660.7666666666,
            "unit": "ns",
            "range": "± 14115.257635484093"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2000760.892857143,
            "unit": "ns",
            "range": "± 14984.935764860125"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1957948.1578947369,
            "unit": "ns",
            "range": "± 43077.545058867036"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1566079.8088235294,
            "unit": "ns",
            "range": "± 16874.41585700509"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1316724.963414634,
            "unit": "ns",
            "range": "± 64391.905918281125"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1564240.9363636363,
            "unit": "ns",
            "range": "± 17507.42228734114"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1331019.9310344828,
            "unit": "ns",
            "range": "± 11907.658372064096"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 6756954.933333334,
            "unit": "ns",
            "range": "± 75068.02281743899"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1377408.732142857,
            "unit": "ns",
            "range": "± 49546.11637270732"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a7207047c365fe18a48f02ee7be3190bafd7dcfa",
          "message": "Merge pull request #106 from marius-bughiu/perf/read-path-unsafe-bce\n\nperf(collections): Unsafe.Add BCE on the dictionary read paths",
          "timestamp": "2026-05-23T09:47:18+03:00",
          "tree_id": "846f96bedb07c4fd2c96110e21369114b7d68809",
          "url": "https://github.com/marius-bughiu/Celerity/commit/a7207047c365fe18a48f02ee7be3190bafd7dcfa"
        },
        "date": 1779520863778,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13989.290108998617,
            "unit": "ns",
            "range": "± 219.07814088619597"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13079.63778159732,
            "unit": "ns",
            "range": "± 321.14292594716784"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12982.491445367987,
            "unit": "ns",
            "range": "± 613.1717100708587"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9247.608711242676,
            "unit": "ns",
            "range": "± 57.17482541624974"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8973.612277853077,
            "unit": "ns",
            "range": "± 92.80200706131806"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9354.957582746234,
            "unit": "ns",
            "range": "± 79.39230351211272"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5209184.973721591,
            "unit": "ns",
            "range": "± 96737.76073468554"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5064934.501478041,
            "unit": "ns",
            "range": "± 116670.55795045225"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4500143.377772178,
            "unit": "ns",
            "range": "± 110391.86363617943"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3236125.7315104166,
            "unit": "ns",
            "range": "± 41435.93045745645"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3174863.38125,
            "unit": "ns",
            "range": "± 30629.259629689637"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6108215.084375,
            "unit": "ns",
            "range": "± 38792.059918165956"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4738.3211596679685,
            "unit": "ns",
            "range": "± 5.575749179162754"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4801.643233959491,
            "unit": "ns",
            "range": "± 74.07124239585443"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4750.732737404959,
            "unit": "ns",
            "range": "± 6.173601332504267"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1860.2303011757988,
            "unit": "ns",
            "range": "± 8.84517056274226"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1802.626562867846,
            "unit": "ns",
            "range": "± 4.8398576141617236"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2005.7493034888957,
            "unit": "ns",
            "range": "± 6.366250525009572"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1492181.1063701923,
            "unit": "ns",
            "range": "± 50936.483256052896"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1552233.5757533482,
            "unit": "ns",
            "range": "± 8239.156789226608"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1602865.4462890625,
            "unit": "ns",
            "range": "± 49483.79238496592"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 590479.1159319197,
            "unit": "ns",
            "range": "± 1585.6919542711119"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 572041.6673289331,
            "unit": "ns",
            "range": "± 1350.0781368735995"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 643135.1287042026,
            "unit": "ns",
            "range": "± 3659.0508427085406"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14110.37527926006,
            "unit": "ns",
            "range": "± 500.3413110066201"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13616.807796083647,
            "unit": "ns",
            "range": "± 595.0245165721506"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15081.213463147482,
            "unit": "ns",
            "range": "± 404.47984314050024"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12624.660037231446,
            "unit": "ns",
            "range": "± 131.86684199636684"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 12010.54614503922,
            "unit": "ns",
            "range": "± 281.21327600451394"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 12225.09754198651,
            "unit": "ns",
            "range": "± 307.2930189684246"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4295110.747321429,
            "unit": "ns",
            "range": "± 79361.12268774265"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4119736.3141163792,
            "unit": "ns",
            "range": "± 69678.07812534414"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5084770.15306713,
            "unit": "ns",
            "range": "± 37219.17025905676"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4920384.0188577585,
            "unit": "ns",
            "range": "± 38465.96441708641"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4837123.685344827,
            "unit": "ns",
            "range": "± 38985.54306847277"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6819395.982219827,
            "unit": "ns",
            "range": "± 99107.64485375868"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4741.513987311001,
            "unit": "ns",
            "range": "± 23.906862286808128"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4733.14286988357,
            "unit": "ns",
            "range": "± 15.301570386848287"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4835.4340171813965,
            "unit": "ns",
            "range": "± 40.32391960836859"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2228.5055215199786,
            "unit": "ns",
            "range": "± 78.29683107643233"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2141.076616695949,
            "unit": "ns",
            "range": "± 3.2382469950259654"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2111.5073535101756,
            "unit": "ns",
            "range": "± 2.4837380302672245"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1609070.2175641742,
            "unit": "ns",
            "range": "± 6950.752336600128"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1587913.6047092015,
            "unit": "ns",
            "range": "± 4526.319841083288"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1626694.0408203125,
            "unit": "ns",
            "range": "± 11810.61066388671"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 704884.6087351831,
            "unit": "ns",
            "range": "± 1376.5485578149878"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 675106.37265625,
            "unit": "ns",
            "range": "± 6715.309072431025"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 703441.1444561298,
            "unit": "ns",
            "range": "± 4642.156793443737"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 88348.97790055249,
            "unit": "ns",
            "range": "± 6355.349447692604"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28038.792899408283,
            "unit": "ns",
            "range": "± 3355.1749242521214"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80220.52673796791,
            "unit": "ns",
            "range": "± 6380.021981611066"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30439.6875,
            "unit": "ns",
            "range": "± 4154.709364618692"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 91984.81212121212,
            "unit": "ns",
            "range": "± 6911.70895582967"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 76779.6809815951,
            "unit": "ns",
            "range": "± 6099.0051331055965"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 144838.4714285714,
            "unit": "ns",
            "range": "± 4437.4344766378"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 138342.26363636364,
            "unit": "ns",
            "range": "± 5066.318934389098"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 93868.60122699387,
            "unit": "ns",
            "range": "± 7270.3176714434"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83904.11320754717,
            "unit": "ns",
            "range": "± 5207.3657215996045"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 94717.44047619047,
            "unit": "ns",
            "range": "± 5225.365456159843"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 83287.27777777778,
            "unit": "ns",
            "range": "± 3192.0736067228427"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2096885.103448276,
            "unit": "ns",
            "range": "± 41472.2354235292"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1733736.5833333333,
            "unit": "ns",
            "range": "± 24597.055444420974"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2051045.1142857142,
            "unit": "ns",
            "range": "± 42609.311291679674"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1717203.3620689656,
            "unit": "ns",
            "range": "± 16717.59707501166"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2121739.0261437907,
            "unit": "ns",
            "range": "± 140405.55359692106"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1966868.7333333334,
            "unit": "ns",
            "range": "± 17599.08773680659"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1780840.3362068965,
            "unit": "ns",
            "range": "± 219575.0760698528"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1322770.6382978724,
            "unit": "ns",
            "range": "± 64241.96290762778"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1583217.2772727273,
            "unit": "ns",
            "range": "± 50597.33834831514"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1322289.2241379311,
            "unit": "ns",
            "range": "± 10785.18535719435"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1989821.5643274854,
            "unit": "ns",
            "range": "± 344087.9071689709"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1430616.896551724,
            "unit": "ns",
            "range": "± 17458.825720094424"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3bf1d0f8f3c007e2d59cb90be8d0eb254b7c8951",
          "message": "Merge pull request #113 from marius-bughiu/feat/native-aot-support\n\nNative AOT & trimming compatibility (#32)",
          "timestamp": "2026-05-23T09:48:30+03:00",
          "tree_id": "e1dadb21f0e8338ac2acf068a7343151708066bd",
          "url": "https://github.com/marius-bughiu/Celerity/commit/3bf1d0f8f3c007e2d59cb90be8d0eb254b7c8951"
        },
        "date": 1779520871015,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13299.165587326575,
            "unit": "ns",
            "range": "± 177.21170195144833"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12121.673945109049,
            "unit": "ns",
            "range": "± 219.12165121100003"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13966.038259983063,
            "unit": "ns",
            "range": "± 313.7701554390868"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9224.667310841878,
            "unit": "ns",
            "range": "± 94.85645519470786"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 11253.958747016059,
            "unit": "ns",
            "range": "± 2635.0526653023458"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9965.735330397083,
            "unit": "ns",
            "range": "± 171.37521703803858"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5162103.939024391,
            "unit": "ns",
            "range": "± 122824.93013134698"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4955076.40625,
            "unit": "ns",
            "range": "± 116192.24629408144"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4442112.723000919,
            "unit": "ns",
            "range": "± 166158.54110639225"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3239334.175646552,
            "unit": "ns",
            "range": "± 56709.37067050581"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3151743.775651042,
            "unit": "ns",
            "range": "± 19442.896734813618"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6160508.971223959,
            "unit": "ns",
            "range": "± 62888.753725141025"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4728.121056168167,
            "unit": "ns",
            "range": "± 23.160027806705237"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4738.5789625379775,
            "unit": "ns",
            "range": "± 9.855574497212773"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4742.322121620178,
            "unit": "ns",
            "range": "± 2.7677281220227865"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1858.662708611324,
            "unit": "ns",
            "range": "± 8.696131539759072"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1800.2973915626262,
            "unit": "ns",
            "range": "± 4.767329018431319"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2004.980251736111,
            "unit": "ns",
            "range": "± 9.567457652798645"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1556766.571640625,
            "unit": "ns",
            "range": "± 12983.842394528843"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1569973.5834635417,
            "unit": "ns",
            "range": "± 19389.363326041843"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1640751.0081492458,
            "unit": "ns",
            "range": "± 9394.761883617968"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 587615.2684100115,
            "unit": "ns",
            "range": "± 467.98854409306057"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 573379.2388873922,
            "unit": "ns",
            "range": "± 2362.696121366324"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 637773.5624348958,
            "unit": "ns",
            "range": "± 1935.035435459366"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15211.443072150736,
            "unit": "ns",
            "range": "± 301.12490712728135"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13590.562411659643,
            "unit": "ns",
            "range": "± 301.7689585411543"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14411.918372521033,
            "unit": "ns",
            "range": "± 325.84576059700447"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12649.46853190455,
            "unit": "ns",
            "range": "± 141.20728103651237"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11682.864935302734,
            "unit": "ns",
            "range": "± 270.7472636850487"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11115.77998461042,
            "unit": "ns",
            "range": "± 575.5975674804389"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4398861.010670732,
            "unit": "ns",
            "range": "± 101273.99696452926"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4226167.341463415,
            "unit": "ns",
            "range": "± 96332.53830998411"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4964319.094288793,
            "unit": "ns",
            "range": "± 50304.67992804671"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5023587.952047414,
            "unit": "ns",
            "range": "± 57602.29358289266"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4785047.34453125,
            "unit": "ns",
            "range": "± 47865.25115546748"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6726139.331597222,
            "unit": "ns",
            "range": "± 98013.50265013616"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4714.134605125145,
            "unit": "ns",
            "range": "± 16.65663643143105"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4730.184529797784,
            "unit": "ns",
            "range": "± 18.435819975632384"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4825.668171201433,
            "unit": "ns",
            "range": "± 36.68480819031568"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2840.4118490219116,
            "unit": "ns",
            "range": "± 702.1612972247315"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2138.8181246439617,
            "unit": "ns",
            "range": "± 3.089159130992245"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2116.121382032122,
            "unit": "ns",
            "range": "± 7.403253990569253"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1601375.2474609376,
            "unit": "ns",
            "range": "± 8381.418906586263"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1604685.6301858837,
            "unit": "ns",
            "range": "± 4751.692895573845"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1584993.1864149305,
            "unit": "ns",
            "range": "± 4399.217492630789"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 735513.2759765625,
            "unit": "ns",
            "range": "± 32408.81097499582"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 675981.5808454241,
            "unit": "ns",
            "range": "± 4617.18664515251"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 696884.5159280711,
            "unit": "ns",
            "range": "± 2644.204959378183"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 86716.58510638298,
            "unit": "ns",
            "range": "± 7220.261910916867"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28196.069832402234,
            "unit": "ns",
            "range": "± 3291.560959891265"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79531.17368421053,
            "unit": "ns",
            "range": "± 7187.517720343804"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26048.544776119405,
            "unit": "ns",
            "range": "± 1312.3305769099518"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82206.95854922279,
            "unit": "ns",
            "range": "± 6244.00407543959"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 76013.4689119171,
            "unit": "ns",
            "range": "± 7463.39379255003"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 147254.78861788617,
            "unit": "ns",
            "range": "± 10153.424703067089"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 134073.385,
            "unit": "ns",
            "range": "± 10403.752254433648"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 98606.09171597633,
            "unit": "ns",
            "range": "± 5549.6430439844735"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83464.67741935483,
            "unit": "ns",
            "range": "± 5944.9060050511125"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 90550.64457831325,
            "unit": "ns",
            "range": "± 3440.897657916729"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 87976.21951219512,
            "unit": "ns",
            "range": "± 4432.732705134625"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2046621.2666666666,
            "unit": "ns",
            "range": "± 18379.99960214125"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1911640.8,
            "unit": "ns",
            "range": "± 327871.1654882143"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2044148.2857142857,
            "unit": "ns",
            "range": "± 14822.360873973717"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1713906.1379310344,
            "unit": "ns",
            "range": "± 16478.016545264476"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2028391.0428571429,
            "unit": "ns",
            "range": "± 39949.27880622897"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1969755.2068965517,
            "unit": "ns",
            "range": "± 11801.035430900938"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1604372.2711864407,
            "unit": "ns",
            "range": "± 35298.85516646568"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1380094.8076923077,
            "unit": "ns",
            "range": "± 7875.889024201552"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6606639.5,
            "unit": "ns",
            "range": "± 37116.06359396883"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1329321.1333333333,
            "unit": "ns",
            "range": "± 15573.888894298496"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1758923.855882353,
            "unit": "ns",
            "range": "± 153059.8187739381"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1409115.7142857143,
            "unit": "ns",
            "range": "± 41087.82514843748"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "972689a7caa695099e29c98f007b08dfa9bedd91",
          "message": "Merge pull request #107 from marius-bughiu/perf/enumerator-unsafe-bce\n\nperf(collections): Unsafe.Add BCE on the enumerator MoveNext paths",
          "timestamp": "2026-05-23T17:39:23+03:00",
          "tree_id": "8781fb611d124c863a31b13e2746666e213f290b",
          "url": "https://github.com/marius-bughiu/Celerity/commit/972689a7caa695099e29c98f007b08dfa9bedd91"
        },
        "date": 1779549412120,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 9943.71393312054,
            "unit": "ns",
            "range": "± 453.6919984140331"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 9685.117858886719,
            "unit": "ns",
            "range": "± 29.36187854616333"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 10185.349073212723,
            "unit": "ns",
            "range": "± 70.75942793720596"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 7309.0035685221355,
            "unit": "ns",
            "range": "± 56.15252423729689"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 7053.50969179215,
            "unit": "ns",
            "range": "± 125.64831314951994"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 7474.063447615679,
            "unit": "ns",
            "range": "± 145.28821925947636"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4299115.7590880105,
            "unit": "ns",
            "range": "± 119104.56714942932"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4227632.64443109,
            "unit": "ns",
            "range": "± 99366.40982013733"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 3952473.90818093,
            "unit": "ns",
            "range": "± 219005.98000219322"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 2717681.054620151,
            "unit": "ns",
            "range": "± 17065.98304834384"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 2728447.732489224,
            "unit": "ns",
            "range": "± 24546.926384480324"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5425194.7421875,
            "unit": "ns",
            "range": "± 47212.83543676012"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3823.8902418348525,
            "unit": "ns",
            "range": "± 186.78439990716316"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3658.7603167020357,
            "unit": "ns",
            "range": "± 8.303717175048183"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3957.723063716182,
            "unit": "ns",
            "range": "± 5.544112193250027"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1494.8601716359456,
            "unit": "ns",
            "range": "± 18.5076996620185"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1494.9407828648884,
            "unit": "ns",
            "range": "± 18.683792610181857"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1622.238076739841,
            "unit": "ns",
            "range": "± 1.7196137850513997"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1168041.8003472222,
            "unit": "ns",
            "range": "± 3239.2094126261004"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1193676.1167160561,
            "unit": "ns",
            "range": "± 23332.02313485068"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1220121.9701772837,
            "unit": "ns",
            "range": "± 1963.4579915408065"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 422305.2522063079,
            "unit": "ns",
            "range": "± 2100.7132859018006"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 382192.44849292655,
            "unit": "ns",
            "range": "± 8149.193011139842"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 465222.06630303606,
            "unit": "ns",
            "range": "± 24000.520648407586"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 10104.201036725726,
            "unit": "ns",
            "range": "± 53.62056912838231"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 10262.592651367188,
            "unit": "ns",
            "range": "± 219.05222462361178"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 11725.482000416723,
            "unit": "ns",
            "range": "± 161.66526790561443"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 6859.780244490679,
            "unit": "ns",
            "range": "± 131.0791068117321"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 6601.681935446603,
            "unit": "ns",
            "range": "± 229.2783909795614"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 6587.92378559819,
            "unit": "ns",
            "range": "± 184.8009264664088"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3384909.6103178877,
            "unit": "ns",
            "range": "± 52217.27709835426"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3359661.9983258927,
            "unit": "ns",
            "range": "± 42918.79662554452"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4180815.844010417,
            "unit": "ns",
            "range": "± 53533.696987282914"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4105561.5384114585,
            "unit": "ns",
            "range": "± 69970.03537677217"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4028014.1927083335,
            "unit": "ns",
            "range": "± 64868.51527829773"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6178572.136197916,
            "unit": "ns",
            "range": "± 64628.95910566204"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3938.834057734563,
            "unit": "ns",
            "range": "± 194.35746986441603"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3571.980626424154,
            "unit": "ns",
            "range": "± 5.416725221017396"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4069.095345069622,
            "unit": "ns",
            "range": "± 141.31132176545694"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 1698.1786205686371,
            "unit": "ns",
            "range": "± 25.29027505234704"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 1608.4409890541663,
            "unit": "ns",
            "range": "± 12.40210200207426"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 1781.3858778476715,
            "unit": "ns",
            "range": "± 3.806827478119372"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1235846.7386997768,
            "unit": "ns",
            "range": "± 4918.648011678593"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1219867.0588191105,
            "unit": "ns",
            "range": "± 15599.970650730835"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1215790.0902054398,
            "unit": "ns",
            "range": "± 2246.870821473359"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 544977.1121026401,
            "unit": "ns",
            "range": "± 6728.136377296006"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 441720.94068025285,
            "unit": "ns",
            "range": "± 14364.138537069319"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 542895.380436198,
            "unit": "ns",
            "range": "± 7500.099431687836"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 61347.85602094241,
            "unit": "ns",
            "range": "± 4802.732262824094"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 20640.380208333332,
            "unit": "ns",
            "range": "± 1738.880459545894"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 57958.73575129534,
            "unit": "ns",
            "range": "± 4723.938968183378"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 19801.177083333332,
            "unit": "ns",
            "range": "± 1206.181152942175"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 68885.35046728973,
            "unit": "ns",
            "range": "± 5327.144694868962"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 59178.164179104475,
            "unit": "ns",
            "range": "± 5014.26493266439"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 115169.85483870968,
            "unit": "ns",
            "range": "± 4404.551300519796"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 111107.06896551725,
            "unit": "ns",
            "range": "± 4893.036626208822"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 66681.88860103628,
            "unit": "ns",
            "range": "± 5014.397951074925"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 64642.9609375,
            "unit": "ns",
            "range": "± 5840.027124191578"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 66043.07329842931,
            "unit": "ns",
            "range": "± 6227.143459959377"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 62376.50675675676,
            "unit": "ns",
            "range": "± 6041.902640473515"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1548481.3,
            "unit": "ns",
            "range": "± 15661.832515209795"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1289033.1166666667,
            "unit": "ns",
            "range": "± 11444.369177628383"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1561113.642857143,
            "unit": "ns",
            "range": "± 37625.26686772765"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1306198.7,
            "unit": "ns",
            "range": "± 17157.933723660237"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1531104.0188679246,
            "unit": "ns",
            "range": "± 41344.71422649806"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1520224.2692307692,
            "unit": "ns",
            "range": "± 22109.95281280843"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 3054033.9696969697,
            "unit": "ns",
            "range": "± 3219423.598551097"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1086199.40625,
            "unit": "ns",
            "range": "± 42548.09885432526"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2303870.2636363637,
            "unit": "ns",
            "range": "± 1633275.7806473414"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1113706.1666666667,
            "unit": "ns",
            "range": "± 9000.655359600147"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1520806.6927710844,
            "unit": "ns",
            "range": "± 233987.0347111764"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1237230.3723404256,
            "unit": "ns",
            "range": "± 59645.63592256505"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "35c53c54ba211842f517847d7e43ca0c982bb86b",
          "message": "Merge pull request #116 from marius-bughiu/feat/uint32-murmur3-hasher\n\nfeat(hashing): add UInt32Murmur3Hasher for uint keys",
          "timestamp": "2026-05-23T17:52:08+03:00",
          "tree_id": "d547f7f8de8cffac9868174e240a40684074baed",
          "url": "https://github.com/marius-bughiu/Celerity/commit/35c53c54ba211842f517847d7e43ca0c982bb86b"
        },
        "date": 1779550049852,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13048.089569963728,
            "unit": "ns",
            "range": "± 241.54835592176016"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13468.236926269532,
            "unit": "ns",
            "range": "± 194.01707936598046"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13903.154281616211,
            "unit": "ns",
            "range": "± 390.4075056617475"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8643.016724650066,
            "unit": "ns",
            "range": "± 86.98144059683815"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8740.520427449545,
            "unit": "ns",
            "range": "± 82.98552216118097"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9691.853605940536,
            "unit": "ns",
            "range": "± 206.3443194636549"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4336922.664507516,
            "unit": "ns",
            "range": "± 329888.6544371825"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4362986.35421875,
            "unit": "ns",
            "range": "± 315700.9776368576"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4016760.5700334823,
            "unit": "ns",
            "range": "± 56546.64440414783"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3142131.2036313657,
            "unit": "ns",
            "range": "± 12119.096763194342"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3210446.781640625,
            "unit": "ns",
            "range": "± 26364.139591516872"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6033585.320052084,
            "unit": "ns",
            "range": "± 72211.42236327918"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4240.64023800554,
            "unit": "ns",
            "range": "± 27.164621476823786"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4007.4696573110728,
            "unit": "ns",
            "range": "± 7.599173843481582"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4297.538840721393,
            "unit": "ns",
            "range": "± 103.93361639666806"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1710.182786517673,
            "unit": "ns",
            "range": "± 2.8644598461145447"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1752.7512674713134,
            "unit": "ns",
            "range": "± 7.071192371065609"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1924.0102757421032,
            "unit": "ns",
            "range": "± 2.506289743867669"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1430714.5474609374,
            "unit": "ns",
            "range": "± 9508.019260488549"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1426867.5491536458,
            "unit": "ns",
            "range": "± 5803.386875213949"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1648150.823392428,
            "unit": "ns",
            "range": "± 83705.70154803513"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 536046.1941338901,
            "unit": "ns",
            "range": "± 5191.373609335334"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 538439.6481933594,
            "unit": "ns",
            "range": "± 4894.8755680757995"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 654519.6381835938,
            "unit": "ns",
            "range": "± 7960.190194938788"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14215.992680867514,
            "unit": "ns",
            "range": "± 312.5023266274529"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13508.455915779903,
            "unit": "ns",
            "range": "± 239.9214827038683"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15510.624714914959,
            "unit": "ns",
            "range": "± 538.4395910273719"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9140.65258178711,
            "unit": "ns",
            "range": "± 123.28563991089281"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8753.59929148356,
            "unit": "ns",
            "range": "± 351.70750428791905"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9281.88476003011,
            "unit": "ns",
            "range": "± 334.87830896903006"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3924038.992327009,
            "unit": "ns",
            "range": "± 57553.6147262714"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3866443.663315717,
            "unit": "ns",
            "range": "± 76345.29132596053"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4741056.350643382,
            "unit": "ns",
            "range": "± 179773.69283729268"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4663303.324032738,
            "unit": "ns",
            "range": "± 151096.3874335261"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4653279.024165784,
            "unit": "ns",
            "range": "± 164389.902910032"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6451653.051724138,
            "unit": "ns",
            "range": "± 50451.456581526516"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4158.22272321913,
            "unit": "ns",
            "range": "± 7.2125872362603385"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4171.745761739797,
            "unit": "ns",
            "range": "± 11.838562831768634"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 6380.411565907796,
            "unit": "ns",
            "range": "± 71.066956478715"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2388.728349821908,
            "unit": "ns",
            "range": "± 281.10958774106945"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2163.548933029175,
            "unit": "ns",
            "range": "± 4.474204342917772"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2355.0078898838588,
            "unit": "ns",
            "range": "± 6.148676857865245"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1498412.6868489583,
            "unit": "ns",
            "range": "± 18507.233446172275"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1501268.9365234375,
            "unit": "ns",
            "range": "± 3054.3172904772164"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1552149.5669450432,
            "unit": "ns",
            "range": "± 5907.039635956742"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 690215.6087123326,
            "unit": "ns",
            "range": "± 7201.490599426239"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 660123.0323612607,
            "unit": "ns",
            "range": "± 6138.9019983443695"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 721721.7256673177,
            "unit": "ns",
            "range": "± 1354.1176919534757"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74378.0989010989,
            "unit": "ns",
            "range": "± 7063.381211134716"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28350.299242424244,
            "unit": "ns",
            "range": "± 4533.804501630728"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 71159.14583333333,
            "unit": "ns",
            "range": "± 6865.708221998371"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29258.40306122449,
            "unit": "ns",
            "range": "± 4886.72104483588"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74745.47474747474,
            "unit": "ns",
            "range": "± 7725.710891303712"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70092.25384615385,
            "unit": "ns",
            "range": "± 8837.718661225432"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 127312.675,
            "unit": "ns",
            "range": "± 11158.060057484396"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 119668.9923857868,
            "unit": "ns",
            "range": "± 8254.318832833778"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 86038.03015075377,
            "unit": "ns",
            "range": "± 7914.418189090947"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 73648.82653061225,
            "unit": "ns",
            "range": "± 6437.618403034416"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 79180.5412371134,
            "unit": "ns",
            "range": "± 6392.447305834924"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 71831.1761658031,
            "unit": "ns",
            "range": "± 6128.700702607144"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1894669.35,
            "unit": "ns",
            "range": "± 19693.605839665408"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1516323.1666666667,
            "unit": "ns",
            "range": "± 16197.571747201713"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1864826.06,
            "unit": "ns",
            "range": "± 28622.720098551083"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1522659.4814814816,
            "unit": "ns",
            "range": "± 25753.43542075111"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1891769.3666666667,
            "unit": "ns",
            "range": "± 31086.50764563782"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1881977.2241379311,
            "unit": "ns",
            "range": "± 14058.269838417304"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1649826.2368421052,
            "unit": "ns",
            "range": "± 50561.884381800104"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1304594.2028985508,
            "unit": "ns",
            "range": "± 64989.75968036171"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1624064.965116279,
            "unit": "ns",
            "range": "± 18638.59016534539"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1330550.1379310344,
            "unit": "ns",
            "range": "± 13818.654776047531"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1727594.105691057,
            "unit": "ns",
            "range": "± 98768.03048819705"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1528757.5555555555,
            "unit": "ns",
            "range": "± 42029.798036144784"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7eb6a04a048041a34c729653dedc851734727301",
          "message": "Merge pull request #115 from marius-bughiu/feat/hash-quality-evaluator\n\nfeat(hashing): add HashQualityEvaluator for comparing hasher distribution",
          "timestamp": "2026-05-23T23:14:22+03:00",
          "tree_id": "dcafe8035a26cc70cd81a727a37fb32ab86c8957",
          "url": "https://github.com/marius-bughiu/Celerity/commit/7eb6a04a048041a34c729653dedc851734727301"
        },
        "date": 1779569178424,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12772.633071899414,
            "unit": "ns",
            "range": "± 93.24018008588344"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12860.122051493327,
            "unit": "ns",
            "range": "± 397.9484634260186"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13534.966013025354,
            "unit": "ns",
            "range": "± 110.9527751646016"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9317.600236037682,
            "unit": "ns",
            "range": "± 143.33475462633638"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 13756.560490926107,
            "unit": "ns",
            "range": "± 4462.423443502577"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9859.125545633251,
            "unit": "ns",
            "range": "± 118.62221795413282"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5257434.935075431,
            "unit": "ns",
            "range": "± 85086.79149229282"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5227748.658333333,
            "unit": "ns",
            "range": "± 67303.99370706022"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5078065.139160156,
            "unit": "ns",
            "range": "± 107971.1153913595"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3486605.6357421875,
            "unit": "ns",
            "range": "± 21531.660864434714"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3534679.9814789873,
            "unit": "ns",
            "range": "± 24493.630322902194"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6757909.748653017,
            "unit": "ns",
            "range": "± 86373.36846999101"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4720.287095947266,
            "unit": "ns",
            "range": "± 7.964885622316494"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4711.3747810081195,
            "unit": "ns",
            "range": "± 4.084490067154043"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5078.003294677735,
            "unit": "ns",
            "range": "± 25.270479706467235"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1903.9982009887694,
            "unit": "ns",
            "range": "± 18.297501442953337"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1922.2585578918456,
            "unit": "ns",
            "range": "± 15.504337140569476"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2092.523691722325,
            "unit": "ns",
            "range": "± 3.4480711925653273"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1514822.5651765047,
            "unit": "ns",
            "range": "± 5776.250340037865"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1521442.759765625,
            "unit": "ns",
            "range": "± 2957.227524807846"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1591377.8035016742,
            "unit": "ns",
            "range": "± 2473.7431988670146"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 553161.9642039331,
            "unit": "ns",
            "range": "± 3571.473150143884"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 498236.74368722097,
            "unit": "ns",
            "range": "± 8162.944398056486"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 587380.1770019531,
            "unit": "ns",
            "range": "± 10716.373741173305"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13158.392549641927,
            "unit": "ns",
            "range": "± 137.9981256988852"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14505.776756286621,
            "unit": "ns",
            "range": "± 218.59964932148955"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16155.884957098191,
            "unit": "ns",
            "range": "± 277.90913299059"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 8832.63674715732,
            "unit": "ns",
            "range": "± 222.61082638417747"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8780.56823512486,
            "unit": "ns",
            "range": "± 314.4242035192516"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9253.76419017354,
            "unit": "ns",
            "range": "± 322.92962553039825"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4324158.023716518,
            "unit": "ns",
            "range": "± 67956.10424775846"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4424363.118534483,
            "unit": "ns",
            "range": "± 68156.50666984434"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5270464.753276209,
            "unit": "ns",
            "range": "± 96099.93884103572"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5119083.65546875,
            "unit": "ns",
            "range": "± 45187.62308180929"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5191341.229166667,
            "unit": "ns",
            "range": "± 65458.69678267606"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7566128.2914959015,
            "unit": "ns",
            "range": "± 261875.82692335342"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4836.427159344708,
            "unit": "ns",
            "range": "± 3.1469819878266283"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4684.805983679636,
            "unit": "ns",
            "range": "± 74.52102533221698"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5034.627680264986,
            "unit": "ns",
            "range": "± 28.59644212867"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2185.265665435791,
            "unit": "ns",
            "range": "± 9.947891610296507"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2077.9671542726715,
            "unit": "ns",
            "range": "± 5.904133341769912"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2217.517285410563,
            "unit": "ns",
            "range": "± 6.958013124512805"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1608325.9126880786,
            "unit": "ns",
            "range": "± 7688.713402822941"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1593182.0122070312,
            "unit": "ns",
            "range": "± 20308.52178616461"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1586817.5825363686,
            "unit": "ns",
            "range": "± 4937.667992622347"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 621495.2485519935,
            "unit": "ns",
            "range": "± 13848.788714878518"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 581534.2918661794,
            "unit": "ns",
            "range": "± 10463.24581243465"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 719161.1496161099,
            "unit": "ns",
            "range": "± 37882.28527809652"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79600.78795811518,
            "unit": "ns",
            "range": "± 8071.646070028193"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25938.418181818182,
            "unit": "ns",
            "range": "± 2002.5385810200332"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 76472.90710382514,
            "unit": "ns",
            "range": "± 6083.724672182927"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26407.59259259259,
            "unit": "ns",
            "range": "± 1689.6423557699738"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82781.32216494845,
            "unit": "ns",
            "range": "± 7540.983411774382"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70632.0824742268,
            "unit": "ns",
            "range": "± 6446.505635433086"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 147816.9867549669,
            "unit": "ns",
            "range": "± 7989.280317597868"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 144902.39880952382,
            "unit": "ns",
            "range": "± 5642.357218450546"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 91336.03353658537,
            "unit": "ns",
            "range": "± 8299.564850026096"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 85343.17512690356,
            "unit": "ns",
            "range": "± 7696.993399130983"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 85130.06153846154,
            "unit": "ns",
            "range": "± 7467.5925069118675"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 77045,
            "unit": "ns",
            "range": "± 6230.390700767531"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2004828.7666666666,
            "unit": "ns",
            "range": "± 23919.699322991055"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1678207.642857143,
            "unit": "ns",
            "range": "± 17527.677527195512"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2005447.8879310344,
            "unit": "ns",
            "range": "± 64388.9887201522"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1672951.9166666667,
            "unit": "ns",
            "range": "± 24872.941634844927"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2047191.4945054946,
            "unit": "ns",
            "range": "± 97056.29212316524"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1941109.0666666667,
            "unit": "ns",
            "range": "± 26342.315597013174"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1705922.044117647,
            "unit": "ns",
            "range": "± 43033.164998077424"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1409206.577669903,
            "unit": "ns",
            "range": "± 55723.12666102973"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1806684.1085271318,
            "unit": "ns",
            "range": "± 100764.20999396004"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1443085.0344827587,
            "unit": "ns",
            "range": "± 18344.044846708544"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1913866.6459627328,
            "unit": "ns",
            "range": "± 205524.62915607612"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1570999.25,
            "unit": "ns",
            "range": "± 20560.185430466943"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8af477f55f5e14bf43053d6b759e7905668f597a",
          "message": "Merge pull request #120 from marius-bughiu/feat/uint32-wang-hasher\n\nfeat(hashing): add UInt32WangHasher for uint keys",
          "timestamp": "2026-05-28T08:27:35+03:00",
          "tree_id": "7482317fdd3dbcbfa57a7cb6b61b026ed9e7c273",
          "url": "https://github.com/marius-bughiu/Celerity/commit/8af477f55f5e14bf43053d6b759e7905668f597a"
        },
        "date": 1779948028470,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12593.428641764323,
            "unit": "ns",
            "range": "± 220.68248448834075"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13450.2630947658,
            "unit": "ns",
            "range": "± 106.05339135677768"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13675.875239405139,
            "unit": "ns",
            "range": "± 190.1258821301516"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9245.850749247784,
            "unit": "ns",
            "range": "± 220.80281378660865"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9380.02935552597,
            "unit": "ns",
            "range": "± 158.2209173296627"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10014.226515671302,
            "unit": "ns",
            "range": "± 122.80648406845772"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5222227.457363697,
            "unit": "ns",
            "range": "± 130570.54815691509"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5249177.461371528,
            "unit": "ns",
            "range": "± 141292.99033287074"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4985900.1012168145,
            "unit": "ns",
            "range": "± 218057.62810269976"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3468316.707170759,
            "unit": "ns",
            "range": "± 14332.778831437286"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3530162.5733816964,
            "unit": "ns",
            "range": "± 14016.667500573953"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6780583.577734375,
            "unit": "ns",
            "range": "± 100761.87987634905"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4728.275224832388,
            "unit": "ns",
            "range": "± 22.496366854821247"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4708.781172343663,
            "unit": "ns",
            "range": "± 5.824558580869537"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5081.716958454677,
            "unit": "ns",
            "range": "± 26.814488895077098"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1916.8576775912582,
            "unit": "ns",
            "range": "± 15.672615472728816"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1915.6205670586949,
            "unit": "ns",
            "range": "± 15.583727927028194"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2091.7373116356985,
            "unit": "ns",
            "range": "± 3.1807002187960802"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1521106.269296875,
            "unit": "ns",
            "range": "± 6490.5414380682105"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1513286.572265625,
            "unit": "ns",
            "range": "± 1814.4702061128203"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1583693.6144386574,
            "unit": "ns",
            "range": "± 19896.47384016847"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 551548.48601423,
            "unit": "ns",
            "range": "± 2475.566271153762"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 491261.3521349677,
            "unit": "ns",
            "range": "± 6768.649806359747"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 577506.5420283565,
            "unit": "ns",
            "range": "± 8904.69598589457"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13859.983703613281,
            "unit": "ns",
            "range": "± 136.35207603092798"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13486.136214036207,
            "unit": "ns",
            "range": "± 258.35600895473914"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15069.425322469075,
            "unit": "ns",
            "range": "± 113.73643517295902"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9088.777085706923,
            "unit": "ns",
            "range": "± 234.77010941696904"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8679.283531482402,
            "unit": "ns",
            "range": "± 233.54013196423963"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 8619.121158599854,
            "unit": "ns",
            "range": "± 52.65068488300074"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4326457.028837316,
            "unit": "ns",
            "range": "± 82899.33906028168"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4436924.263091216,
            "unit": "ns",
            "range": "± 97144.62039593268"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5255391.944475447,
            "unit": "ns",
            "range": "± 53291.87909000687"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5137959.8952047415,
            "unit": "ns",
            "range": "± 60628.11598880159"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5183086.4237607755,
            "unit": "ns",
            "range": "± 48770.58625553905"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7631148.77662037,
            "unit": "ns",
            "range": "± 89855.95730701696"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4719.974197998047,
            "unit": "ns",
            "range": "± 4.326767038629935"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4778.578379821777,
            "unit": "ns",
            "range": "± 184.44426193523623"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5113.28617477417,
            "unit": "ns",
            "range": "± 84.25850645219762"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2217.450488917033,
            "unit": "ns",
            "range": "± 14.22165228702212"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2098.5325368245444,
            "unit": "ns",
            "range": "± 14.242599130431087"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2283.1800834110804,
            "unit": "ns",
            "range": "± 11.234327383719885"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1654129.2091290508,
            "unit": "ns",
            "range": "± 51202.931163213645"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1547576.3785445602,
            "unit": "ns",
            "range": "± 2150.610013572845"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1627860.2084263393,
            "unit": "ns",
            "range": "± 23900.17896518617"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 712139.7416811342,
            "unit": "ns",
            "range": "± 6388.701143113741"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 728587.8182583513,
            "unit": "ns",
            "range": "± 51764.799275087964"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 713068.1913006756,
            "unit": "ns",
            "range": "± 24334.51200329571"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82123.61025641026,
            "unit": "ns",
            "range": "± 11448.510831756357"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28268.847826086956,
            "unit": "ns",
            "range": "± 3192.7683542531972"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81717.78142076502,
            "unit": "ns",
            "range": "± 7736.443164970437"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26576.291666666668,
            "unit": "ns",
            "range": "± 1444.8897456875325"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81238.99744897959,
            "unit": "ns",
            "range": "± 7542.596086687694"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70881.97142857143,
            "unit": "ns",
            "range": "± 4867.406567627291"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 149634.98711340205,
            "unit": "ns",
            "range": "± 14504.9570683795"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 141099.63333333333,
            "unit": "ns",
            "range": "± 11446.546310045094"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 86638.55641025641,
            "unit": "ns",
            "range": "± 7856.6920775502385"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83078.7297979798,
            "unit": "ns",
            "range": "± 9572.874576973627"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 87749.44764397906,
            "unit": "ns",
            "range": "± 7256.317024161716"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 76602.15608465609,
            "unit": "ns",
            "range": "± 4856.909134815997"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2005334.0344827587,
            "unit": "ns",
            "range": "± 22729.968618752704"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1674438.3833333333,
            "unit": "ns",
            "range": "± 14020.573060428596"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2006266.4642857143,
            "unit": "ns",
            "range": "± 12190.011720992581"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1675730.2586206896,
            "unit": "ns",
            "range": "± 11661.716701317951"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1995552.4142857143,
            "unit": "ns",
            "range": "± 39486.682868011434"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 2018195.1170212766,
            "unit": "ns",
            "range": "± 69338.34831330733"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1703322.8113207547,
            "unit": "ns",
            "range": "± 38080.74886800851"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1419351.346491228,
            "unit": "ns",
            "range": "± 60280.947039972416"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6378302.206896552,
            "unit": "ns",
            "range": "± 32071.88332968315"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1439886.7142857143,
            "unit": "ns",
            "range": "± 9853.168848638918"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1828874.0222222223,
            "unit": "ns",
            "range": "± 97654.33541846406"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1552730.298076923,
            "unit": "ns",
            "range": "± 67808.42234336893"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0bc59e269c6acf392ae5cbe3f94c1a475f31d32a",
          "message": "Merge pull request #118 from marius-bughiu/fix/indexer-setter-spurious-resize\n\nfix(collections): don't resize on indexer-setter overwrite at threshold",
          "timestamp": "2026-05-28T08:26:44+03:00",
          "tree_id": "a41c6f37204ff279403b1be8e9b6b4c345e5d227",
          "url": "https://github.com/marius-bughiu/Celerity/commit/0bc59e269c6acf392ae5cbe3f94c1a475f31d32a"
        },
        "date": 1779948050428,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12818.659092712402,
            "unit": "ns",
            "range": "± 206.7047682737902"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12722.735082692114,
            "unit": "ns",
            "range": "± 168.89650972857953"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13372.49316932415,
            "unit": "ns",
            "range": "± 157.1667925205932"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9154.127776554653,
            "unit": "ns",
            "range": "± 143.0485901034521"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9364.901974995932,
            "unit": "ns",
            "range": "± 96.3578785807165"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9833.430712444026,
            "unit": "ns",
            "range": "± 220.5388004951139"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5301151.251775568,
            "unit": "ns",
            "range": "± 135265.2586248804"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5196759.461890244,
            "unit": "ns",
            "range": "± 122537.46247708924"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5122697.1022727275,
            "unit": "ns",
            "range": "± 162345.59041963064"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3460033.947405134,
            "unit": "ns",
            "range": "± 17496.31932804277"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3570690.67843192,
            "unit": "ns",
            "range": "± 21292.678125540984"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6803644.575892857,
            "unit": "ns",
            "range": "± 118650.70727978834"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5030.891759138841,
            "unit": "ns",
            "range": "± 303.2650048072168"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4731.935974910341,
            "unit": "ns",
            "range": "± 24.79427088109386"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5958.779552166278,
            "unit": "ns",
            "range": "± 850.4007562403651"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1922.5560962677002,
            "unit": "ns",
            "range": "± 20.555465934478118"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1923.696960054595,
            "unit": "ns",
            "range": "± 15.082779885775626"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2091.751919232882,
            "unit": "ns",
            "range": "± 3.4889134927247714"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1468810.261200574,
            "unit": "ns",
            "range": "± 49837.96154660088"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1508550.4966947115,
            "unit": "ns",
            "range": "± 2245.528902798505"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1575045.9997746395,
            "unit": "ns",
            "range": "± 2889.553646149405"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 549717.4270207331,
            "unit": "ns",
            "range": "± 1917.2097449673927"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 499523.2683026714,
            "unit": "ns",
            "range": "± 11374.037081998451"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 594684.4214509663,
            "unit": "ns",
            "range": "± 19847.229770270405"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13874.311616163988,
            "unit": "ns",
            "range": "± 367.09222327445815"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13699.221930226971,
            "unit": "ns",
            "range": "± 343.40840921381385"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15114.31261154701,
            "unit": "ns",
            "range": "± 213.51940970730197"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 8941.872007446289,
            "unit": "ns",
            "range": "± 411.6806708547005"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8765.630399621052,
            "unit": "ns",
            "range": "± 431.7815776249285"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 8656.566605122884,
            "unit": "ns",
            "range": "± 145.75797390515757"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4407255.619554924,
            "unit": "ns",
            "range": "± 87718.51161930474"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4379930.117919922,
            "unit": "ns",
            "range": "± 87304.15921890017"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5328144.062630208,
            "unit": "ns",
            "range": "± 76760.77575741119"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5156018.782118056,
            "unit": "ns",
            "range": "± 50738.620937789434"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5231993.952083333,
            "unit": "ns",
            "range": "± 51630.09357574815"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7650675.859953703,
            "unit": "ns",
            "range": "± 47034.182920921565"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4720.180893509476,
            "unit": "ns",
            "range": "± 5.637066638940278"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4601.829766167535,
            "unit": "ns",
            "range": "± 3.137315502788676"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5035.900592803955,
            "unit": "ns",
            "range": "± 36.032942956017195"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2756.964943237305,
            "unit": "ns",
            "range": "± 506.9186661421107"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2100.837480545044,
            "unit": "ns",
            "range": "± 6.578925647659003"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2283.790885772705,
            "unit": "ns",
            "range": "± 3.6539633575327546"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1603132.34211077,
            "unit": "ns",
            "range": "± 6844.450520752695"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1537974.1910574776,
            "unit": "ns",
            "range": "± 1485.7246189708108"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1595978.7144396552,
            "unit": "ns",
            "range": "± 4019.0397214924387"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 704668.8839911099,
            "unit": "ns",
            "range": "± 4397.344513162453"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 728378.3254231771,
            "unit": "ns",
            "range": "± 43257.42945968761"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 673385.9084852431,
            "unit": "ns",
            "range": "± 35436.00382685153"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82564.5794871795,
            "unit": "ns",
            "range": "± 9684.109001459781"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27372.016129032258,
            "unit": "ns",
            "range": "± 1023.6814388569229"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 78536.25129533678,
            "unit": "ns",
            "range": "± 6999.483115798243"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27813.572916666668,
            "unit": "ns",
            "range": "± 2758.3596337317704"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83505.7358974359,
            "unit": "ns",
            "range": "± 6589.264028623449"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 71311.77710843373,
            "unit": "ns",
            "range": "± 4059.7527561327365"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 146933.0875,
            "unit": "ns",
            "range": "± 7894.139531425443"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 154214.90101522842,
            "unit": "ns",
            "range": "± 17399.687714594398"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 90131.26424870467,
            "unit": "ns",
            "range": "± 8043.065504262079"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 84907.86010362694,
            "unit": "ns",
            "range": "± 7421.90793404019"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 83721.64917127072,
            "unit": "ns",
            "range": "± 5320.662058570644"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 74164.88095238095,
            "unit": "ns",
            "range": "± 5581.869343216627"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2006548.2758620689,
            "unit": "ns",
            "range": "± 18624.899786205086"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1683757.5333333334,
            "unit": "ns",
            "range": "± 13989.955079870042"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2002306.6666666667,
            "unit": "ns",
            "range": "± 24073.665480731448"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1681361.85,
            "unit": "ns",
            "range": "± 12681.10328208323"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2041473.91005291,
            "unit": "ns",
            "range": "± 139329.17261164697"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1936688.8703703703,
            "unit": "ns",
            "range": "± 13002.793437737639"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1703401.5185185184,
            "unit": "ns",
            "range": "± 21499.2685235503"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1417268.7079646017,
            "unit": "ns",
            "range": "± 60327.56243109977"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1708382.0166666666,
            "unit": "ns",
            "range": "± 17672.54646458218"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1437007.232142857,
            "unit": "ns",
            "range": "± 12710.182364925742"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1884678.1347305388,
            "unit": "ns",
            "range": "± 235802.96937121352"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1691770.652892562,
            "unit": "ns",
            "range": "± 137302.92975265253"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "4287b89b52510899a5e001b8e26f7b4ec527011e",
          "message": "Merge pull request #121 from marius-bughiu/feat/uint64-wang-hasher\n\nfeat(hashing): add UInt64WangHasher for ulong keys",
          "timestamp": "2026-05-31T09:18:17+03:00",
          "tree_id": "c6c18f941186e21097faee837ebe26da2994630b",
          "url": "https://github.com/marius-bughiu/Celerity/commit/4287b89b52510899a5e001b8e26f7b4ec527011e"
        },
        "date": 1780210279095,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12099.372404953529,
            "unit": "ns",
            "range": "± 156.4419649788246"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12191.977285977067,
            "unit": "ns",
            "range": "± 142.88091322073427"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12941.900416933257,
            "unit": "ns",
            "range": "± 196.92663230289406"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8736.414577738444,
            "unit": "ns",
            "range": "± 73.48128204704544"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8686.339285786946,
            "unit": "ns",
            "range": "± 168.23152802284304"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9019.75067243905,
            "unit": "ns",
            "range": "± 201.3943815457339"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4961006.585365853,
            "unit": "ns",
            "range": "± 121264.88343531167"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4986364.848177084,
            "unit": "ns",
            "range": "± 101063.62391976184"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4523402.058327415,
            "unit": "ns",
            "range": "± 185079.47854847644"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3156428.10546875,
            "unit": "ns",
            "range": "± 18076.819072199065"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3159483.6772135417,
            "unit": "ns",
            "range": "± 22442.98102390264"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6052686.3578125,
            "unit": "ns",
            "range": "± 43419.007760525856"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4930.681635256166,
            "unit": "ns",
            "range": "± 187.40631064687838"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4737.528378601074,
            "unit": "ns",
            "range": "± 4.509249491630035"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4747.352041173865,
            "unit": "ns",
            "range": "± 6.046538077157432"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1831.3166801189554,
            "unit": "ns",
            "range": "± 27.93691836995411"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1801.618234316508,
            "unit": "ns",
            "range": "± 4.97289740366562"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2005.0336699309173,
            "unit": "ns",
            "range": "± 9.912907004749497"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1553952.6655649038,
            "unit": "ns",
            "range": "± 5407.951099217222"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1576642.6264322917,
            "unit": "ns",
            "range": "± 6207.929968954172"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1639679.124057112,
            "unit": "ns",
            "range": "± 9081.661451101047"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 587872.8306169182,
            "unit": "ns",
            "range": "± 541.2922270382787"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 572162.4826388889,
            "unit": "ns",
            "range": "± 2619.569130026267"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 637859.0815429688,
            "unit": "ns",
            "range": "± 1042.179426100168"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14243.157724761963,
            "unit": "ns",
            "range": "± 1146.4175927724505"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14212.936120986938,
            "unit": "ns",
            "range": "± 834.6971162199408"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14917.114226640973,
            "unit": "ns",
            "range": "± 519.4042666760917"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11647.89229767898,
            "unit": "ns",
            "range": "± 84.31195781232003"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11101.122086097454,
            "unit": "ns",
            "range": "± 103.76765513105903"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11358.969730245655,
            "unit": "ns",
            "range": "± 135.89441818373803"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4070550.122479839,
            "unit": "ns",
            "range": "± 83056.57040237941"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4131098.6396169355,
            "unit": "ns",
            "range": "± 68205.07706318637"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4993607.285807292,
            "unit": "ns",
            "range": "± 70376.84730762806"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4738848.670528017,
            "unit": "ns",
            "range": "± 39784.536620547726"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4871934.168489584,
            "unit": "ns",
            "range": "± 37724.70022824803"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6745282.484240302,
            "unit": "ns",
            "range": "± 63794.9800083185"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4780.676147206625,
            "unit": "ns",
            "range": "± 18.68685299671763"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4727.961362566267,
            "unit": "ns",
            "range": "± 19.03185894238681"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4892.80240368021,
            "unit": "ns",
            "range": "± 38.88167794316176"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2290.5519761357987,
            "unit": "ns",
            "range": "± 84.77528359429567"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 5354.071764285748,
            "unit": "ns",
            "range": "± 3541.574186956371"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2268.9008828622323,
            "unit": "ns",
            "range": "± 6.534642788280321"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1591392.171802662,
            "unit": "ns",
            "range": "± 7554.589964325239"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1593833.367115162,
            "unit": "ns",
            "range": "± 2598.673737825938"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1589078.7919596354,
            "unit": "ns",
            "range": "± 4679.566275337902"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 717063.2042643229,
            "unit": "ns",
            "range": "± 23494.721734007773"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 666468.670297476,
            "unit": "ns",
            "range": "± 7493.109603756482"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 695129.271897536,
            "unit": "ns",
            "range": "± 994.1135415493335"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 86746.49742268042,
            "unit": "ns",
            "range": "± 12368.244612161536"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26032.22142857143,
            "unit": "ns",
            "range": "± 1405.8723964929984"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80428.44502617801,
            "unit": "ns",
            "range": "± 6377.297073531014"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25451.00588235294,
            "unit": "ns",
            "range": "± 1336.903938948879"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81353.30481283422,
            "unit": "ns",
            "range": "± 7380.39990142653"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 76087.24352331606,
            "unit": "ns",
            "range": "± 5490.060906118211"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 146175.83582089553,
            "unit": "ns",
            "range": "± 11728.63954955428"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 140154.5754716981,
            "unit": "ns",
            "range": "± 6917.522554518446"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 94000.05487804877,
            "unit": "ns",
            "range": "± 7767.059505698929"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 82686.23684210527,
            "unit": "ns",
            "range": "± 6638.455067585358"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 96260.32911392405,
            "unit": "ns",
            "range": "± 5270.862170319054"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 80798.77037037037,
            "unit": "ns",
            "range": "± 4611.586352404419"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2041247.3448275863,
            "unit": "ns",
            "range": "± 25651.726713706907"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1700792.3,
            "unit": "ns",
            "range": "± 13258.428960216343"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2043451.1896551724,
            "unit": "ns",
            "range": "± 14449.864880988258"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1705599.0961538462,
            "unit": "ns",
            "range": "± 8279.319984176514"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1972123.3214285714,
            "unit": "ns",
            "range": "± 20111.103949759752"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1972510.3793103448,
            "unit": "ns",
            "range": "± 17587.79173869882"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1552772.3958333333,
            "unit": "ns",
            "range": "± 17143.01587954597"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1281554.863095238,
            "unit": "ns",
            "range": "± 39024.17996632709"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1564241.7433628319,
            "unit": "ns",
            "range": "± 47028.963776474026"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1324396.5892857143,
            "unit": "ns",
            "range": "± 8305.090568611755"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 2949488.9827586208,
            "unit": "ns",
            "range": "± 2282936.0969434795"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1404998.9724770642,
            "unit": "ns",
            "range": "± 81993.44062083156"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a2ef357dcab854709664927426d0ef98478b7a84",
          "message": "Merge pull request #122 from marius-bughiu/chore/code-review/int-collection-hasher-escalation-docs\n\ndocs(collections): mention hasher escalation tier on IntDictionary/IntSet",
          "timestamp": "2026-05-31T09:20:04+03:00",
          "tree_id": "50d1c728d4327b889861d6fa5e2f5eaed50ec0ae",
          "url": "https://github.com/marius-bughiu/Celerity/commit/a2ef357dcab854709664927426d0ef98478b7a84"
        },
        "date": 1780210432326,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12418.460391325109,
            "unit": "ns",
            "range": "± 320.70524291707073"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12032.540386199951,
            "unit": "ns",
            "range": "± 250.22544729102958"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13198.332370626515,
            "unit": "ns",
            "range": "± 606.0046092969736"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8970.602345959893,
            "unit": "ns",
            "range": "± 84.45789841327394"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8603.148282368978,
            "unit": "ns",
            "range": "± 91.03632366097294"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9122.01768221174,
            "unit": "ns",
            "range": "± 62.369598847811716"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5097921.23460478,
            "unit": "ns",
            "range": "± 143575.56872160634"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4999080.0939611485,
            "unit": "ns",
            "range": "± 134579.22498836034"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4569561.85546875,
            "unit": "ns",
            "range": "± 218527.78022792152"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3187772.360546875,
            "unit": "ns",
            "range": "± 28338.34590267269"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3186633.901953125,
            "unit": "ns",
            "range": "± 27007.275570436348"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6068867.0358297415,
            "unit": "ns",
            "range": "± 51258.50422744242"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4767.213483613113,
            "unit": "ns",
            "range": "± 30.483866715088336"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4913.56556731004,
            "unit": "ns",
            "range": "± 182.2568407837919"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4795.697070922852,
            "unit": "ns",
            "range": "± 52.28603685559678"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1827.2857580820719,
            "unit": "ns",
            "range": "± 39.438020671284896"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1800.3002659933907,
            "unit": "ns",
            "range": "± 6.776322041693059"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2007.895025507609,
            "unit": "ns",
            "range": "± 9.15751420238168"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1566399.2569754464,
            "unit": "ns",
            "range": "± 17946.072256398435"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1544989.939140625,
            "unit": "ns",
            "range": "± 2267.475281307328"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1638001.0700431035,
            "unit": "ns",
            "range": "± 11126.472632490933"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 589304.0224971065,
            "unit": "ns",
            "range": "± 1630.4528804765414"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 570470.1100376674,
            "unit": "ns",
            "range": "± 1650.2316273116962"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 638553.0434027778,
            "unit": "ns",
            "range": "± 2426.9282970150457"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14743.75718679989,
            "unit": "ns",
            "range": "± 649.5788060124406"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 12955.000372823079,
            "unit": "ns",
            "range": "± 132.29706609439413"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14983.04856669108,
            "unit": "ns",
            "range": "± 269.37354265732085"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11629.759073893229,
            "unit": "ns",
            "range": "± 54.33009585484452"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11180.888688712284,
            "unit": "ns",
            "range": "± 71.4543451017754"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11220.546839220771,
            "unit": "ns",
            "range": "± 40.80593434811138"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4175302.25765625,
            "unit": "ns",
            "range": "± 116808.86449495092"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4250463.972782258,
            "unit": "ns",
            "range": "± 156135.75426955003"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4963668.171875,
            "unit": "ns",
            "range": "± 44612.600644023376"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4850755.164192708,
            "unit": "ns",
            "range": "± 57995.65179005305"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4842884.787715517,
            "unit": "ns",
            "range": "± 31180.041176541756"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6740096.666666667,
            "unit": "ns",
            "range": "± 66446.35385767762"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4768.996644428798,
            "unit": "ns",
            "range": "± 29.97053553260569"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4758.402710750185,
            "unit": "ns",
            "range": "± 24.165520095670615"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4889.128525870187,
            "unit": "ns",
            "range": "± 23.860577396487432"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2218.100920500579,
            "unit": "ns",
            "range": "± 7.624132500419492"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2144.157431738717,
            "unit": "ns",
            "range": "± 9.271148579692952"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2262.61971901203,
            "unit": "ns",
            "range": "± 17.190831349042472"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1631831.1962890625,
            "unit": "ns",
            "range": "± 40769.543277927136"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1624749.0598144531,
            "unit": "ns",
            "range": "± 12275.627633792878"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1593341.193359375,
            "unit": "ns",
            "range": "± 8090.7211440367355"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 692107.1219726562,
            "unit": "ns",
            "range": "± 2390.715665307421"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 715159.6522565569,
            "unit": "ns",
            "range": "± 26181.98530761147"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 700306.3303475216,
            "unit": "ns",
            "range": "± 2234.747015034232"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 84465.3947368421,
            "unit": "ns",
            "range": "± 8358.540514419312"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26904.510638297874,
            "unit": "ns",
            "range": "± 1444.462893836921"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82003.9053030303,
            "unit": "ns",
            "range": "± 5758.472991615269"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30397.666666666668,
            "unit": "ns",
            "range": "± 3646.12110556765"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 87690.8286516854,
            "unit": "ns",
            "range": "± 5659.785465641259"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 75879.53260869565,
            "unit": "ns",
            "range": "± 6080.115635496365"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 150432.77586206896,
            "unit": "ns",
            "range": "± 18528.658897636862"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 137956.19491525425,
            "unit": "ns",
            "range": "± 10789.364419315643"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 134103.33684210526,
            "unit": "ns",
            "range": "± 24876.5145749152"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83962.73412698413,
            "unit": "ns",
            "range": "± 5926.90286462888"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 96111.31176470588,
            "unit": "ns",
            "range": "± 5338.069161678214"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 82871.01832460733,
            "unit": "ns",
            "range": "± 6888.413889578365"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2047693.5,
            "unit": "ns",
            "range": "± 20670.61975544212"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1712312.1896551724,
            "unit": "ns",
            "range": "± 19529.992423931395"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2058322,
            "unit": "ns",
            "range": "± 15053.162594261901"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1709109.9827586208,
            "unit": "ns",
            "range": "± 12817.548239956841"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1973591.1,
            "unit": "ns",
            "range": "± 26562.388418718987"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1976666.551724138,
            "unit": "ns",
            "range": "± 14743.715795905837"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1574211.857142857,
            "unit": "ns",
            "range": "± 34687.55819334458"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1308902.9479166667,
            "unit": "ns",
            "range": "± 49266.040837449655"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6727111.916666667,
            "unit": "ns",
            "range": "± 135452.89114447866"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1335773.8666666667,
            "unit": "ns",
            "range": "± 13859.744192626986"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1593007.3272727274,
            "unit": "ns",
            "range": "± 31894.699233775686"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1435120.2962962964,
            "unit": "ns",
            "range": "± 13663.904306926037"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e5a2d8dca8059065cb9f48669b1f176a3fb95d51",
          "message": "Merge pull request #124 from marius-bughiu/chore/issue-123-xunit2013-cleanup\n\nchore(tests): clean up xUnit2013 warnings (#123)",
          "timestamp": "2026-05-31T09:28:00+03:00",
          "tree_id": "c2276c79d996b401d3cbfa4cf4fe9aa1ed90ce05",
          "url": "https://github.com/marius-bughiu/Celerity/commit/e5a2d8dca8059065cb9f48669b1f176a3fb95d51"
        },
        "date": 1780210845061,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13269.573688648365,
            "unit": "ns",
            "range": "± 155.48298748315165"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13127.38486565484,
            "unit": "ns",
            "range": "± 129.5585472488539"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13790.72938639323,
            "unit": "ns",
            "range": "± 281.9545715335953"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9455.895403544107,
            "unit": "ns",
            "range": "± 130.09397288880828"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9742.801392295143,
            "unit": "ns",
            "range": "± 316.65923684417504"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9867.013224283854,
            "unit": "ns",
            "range": "± 142.02759110243153"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5270271.157628677,
            "unit": "ns",
            "range": "± 97106.65292560031"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5239735.267057291,
            "unit": "ns",
            "range": "± 90738.38454188737"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5107011.311961207,
            "unit": "ns",
            "range": "± 103427.58768574508"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3490156.230208333,
            "unit": "ns",
            "range": "± 11884.135819620524"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3574834.8373046876,
            "unit": "ns",
            "range": "± 30992.583530464053"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6806095.842881944,
            "unit": "ns",
            "range": "± 79014.23959724799"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4708.950360333478,
            "unit": "ns",
            "range": "± 6.9079869013939215"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4721.312763637967,
            "unit": "ns",
            "range": "± 9.530537522108153"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5055.39872430872,
            "unit": "ns",
            "range": "± 3.6180867313092957"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1911.8646807177313,
            "unit": "ns",
            "range": "± 18.794895831511536"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1918.5039716084798,
            "unit": "ns",
            "range": "± 17.116032802143515"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2094.8539090649833,
            "unit": "ns",
            "range": "± 5.550871573137546"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1512467.2366536458,
            "unit": "ns",
            "range": "± 2225.7131100060647"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1506019.182466947,
            "unit": "ns",
            "range": "± 5569.046006701367"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1567988.7394935344,
            "unit": "ns",
            "range": "± 5211.336674553171"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 549091.0715332031,
            "unit": "ns",
            "range": "± 2726.5662628033688"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 494077.27888371394,
            "unit": "ns",
            "range": "± 6319.572363253562"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 579712.4380754743,
            "unit": "ns",
            "range": "± 10605.640469644104"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14905.046982337688,
            "unit": "ns",
            "range": "± 104.23305183089812"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15156.981945582798,
            "unit": "ns",
            "range": "± 130.41032897108767"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16197.938174017545,
            "unit": "ns",
            "range": "± 147.74090674870786"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9731.965287272136,
            "unit": "ns",
            "range": "± 172.8821627912831"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9549.490828403206,
            "unit": "ns",
            "range": "± 296.81517978449256"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9317.9317939395,
            "unit": "ns",
            "range": "± 215.76502268595752"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4492851.908922697,
            "unit": "ns",
            "range": "± 96452.0958504224"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4535146.420636433,
            "unit": "ns",
            "range": "± 101487.65842529266"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5485173.704180744,
            "unit": "ns",
            "range": "± 185566.0576886525"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5209279.147898707,
            "unit": "ns",
            "range": "± 72103.4187528287"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5375176.259259259,
            "unit": "ns",
            "range": "± 84928.98927927598"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7752996.386979166,
            "unit": "ns",
            "range": "± 105203.30321061568"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4657.269408886249,
            "unit": "ns",
            "range": "± 57.87653244129354"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4605.887581787109,
            "unit": "ns",
            "range": "± 6.3706104282715845"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5037.71605471907,
            "unit": "ns",
            "range": "± 39.535576849950345"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2222.5092849731445,
            "unit": "ns",
            "range": "± 6.139002193579197"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2080.892716544015,
            "unit": "ns",
            "range": "± 6.518674580057355"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2284.368437400231,
            "unit": "ns",
            "range": "± 12.47101724408131"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1608379.751663773,
            "unit": "ns",
            "range": "± 8708.8595983981"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1535796.5110426683,
            "unit": "ns",
            "range": "± 4792.294169630057"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1602034.4008620689,
            "unit": "ns",
            "range": "± 16764.96897957751"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 701307.439592634,
            "unit": "ns",
            "range": "± 4846.2534972719"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 723482.4880455281,
            "unit": "ns",
            "range": "± 37455.87271127215"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 662056.1805594309,
            "unit": "ns",
            "range": "± 46295.66469735056"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79608.72043010753,
            "unit": "ns",
            "range": "± 6433.226720101587"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27789.120253164558,
            "unit": "ns",
            "range": "± 946.6812009600789"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 86698.63571428572,
            "unit": "ns",
            "range": "± 10255.177812201326"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26901.364485981307,
            "unit": "ns",
            "range": "± 1670.1792548272056"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 85906.13756613756,
            "unit": "ns",
            "range": "± 9066.399332332287"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 72286.46335078534,
            "unit": "ns",
            "range": "± 7760.499428700468"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 149860.22222222222,
            "unit": "ns",
            "range": "± 3081.0214434373274"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 144091.48214285713,
            "unit": "ns",
            "range": "± 9311.526614689701"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 87942.78342245989,
            "unit": "ns",
            "range": "± 6602.058486407819"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 81118.22164948453,
            "unit": "ns",
            "range": "± 6996.777114278146"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 93094.29787234042,
            "unit": "ns",
            "range": "± 10454.864557266996"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 87298.75757575757,
            "unit": "ns",
            "range": "± 14138.502678920637"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2372216.380319149,
            "unit": "ns",
            "range": "± 214438.9232347286"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1672185.4827586208,
            "unit": "ns",
            "range": "± 16422.154216659994"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2050521.4891304348,
            "unit": "ns",
            "range": "± 79174.30991666488"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1713543.423076923,
            "unit": "ns",
            "range": "± 17699.746572588156"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2043105.2112676057,
            "unit": "ns",
            "range": "± 99735.12124603921"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1932022.5535714286,
            "unit": "ns",
            "range": "± 27791.559036386392"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 2022122.3135135134,
            "unit": "ns",
            "range": "± 308342.47934947175"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1445947.022580645,
            "unit": "ns",
            "range": "± 103577.65553346701"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2626713.4591836734,
            "unit": "ns",
            "range": "± 1575544.8221252786"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1479232.7352941176,
            "unit": "ns",
            "range": "± 29624.72489790061"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1965514.4590643274,
            "unit": "ns",
            "range": "± 250591.48082104974"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1560133.8620689656,
            "unit": "ns",
            "range": "± 18504.51662138219"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "da8d50168d3d317e04cc9a1011603b2d6d7fa1f6",
          "message": "Merge pull request #126 from marius-bughiu/feat/issue-24-uint64-wang-naive-hasher\n\nfeat(hashing): add UInt64WangNaiveHasher for ulong keys",
          "timestamp": "2026-06-01T13:09:07+03:00",
          "tree_id": "e22caa2a2246a9734273fb921ac81276da553a61",
          "url": "https://github.com/marius-bughiu/Celerity/commit/da8d50168d3d317e04cc9a1011603b2d6d7fa1f6"
        },
        "date": 1780310611843,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13266.48646763393,
            "unit": "ns",
            "range": "± 144.6939696962625"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13267.753486370219,
            "unit": "ns",
            "range": "± 235.23078177795273"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14363.319889683877,
            "unit": "ns",
            "range": "± 239.05536130949602"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9065.778522491455,
            "unit": "ns",
            "range": "± 37.30661726563428"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9058.710656527815,
            "unit": "ns",
            "range": "± 74.89758982917478"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10043.893744877407,
            "unit": "ns",
            "range": "± 135.18879771640613"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5057391.739630681,
            "unit": "ns",
            "range": "± 153778.3353035057"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5082353.560546875,
            "unit": "ns",
            "range": "± 125308.32727959071"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4462854.729182546,
            "unit": "ns",
            "range": "± 205115.8874825413"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3189394.9191810344,
            "unit": "ns",
            "range": "± 28506.90471197338"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3181164.3760775863,
            "unit": "ns",
            "range": "± 32769.435130069236"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6200280.934430803,
            "unit": "ns",
            "range": "± 44768.57530997385"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4744.462546171965,
            "unit": "ns",
            "range": "± 12.250591866851117"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4734.032776184082,
            "unit": "ns",
            "range": "± 5.741812159518815"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5128.782220345956,
            "unit": "ns",
            "range": "± 304.73357042582245"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1864.9352685928345,
            "unit": "ns",
            "range": "± 12.408790767539152"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1799.040237952923,
            "unit": "ns",
            "range": "± 8.489069171220367"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2003.1634961536952,
            "unit": "ns",
            "range": "± 10.006378824988772"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1520288.5636284722,
            "unit": "ns",
            "range": "± 42592.05094932914"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1574361.2178744613,
            "unit": "ns",
            "range": "± 3489.2367277547105"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1594553.1099283854,
            "unit": "ns",
            "range": "± 41152.71512403194"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 588255.8028470553,
            "unit": "ns",
            "range": "± 1346.463126888861"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 572739.1592122396,
            "unit": "ns",
            "range": "± 2308.9390195619817"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 644568.3078613281,
            "unit": "ns",
            "range": "± 999.2859533352432"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13393.99666243333,
            "unit": "ns",
            "range": "± 119.51467710796616"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14859.167657470703,
            "unit": "ns",
            "range": "± 500.14993176016225"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 17742.45010002984,
            "unit": "ns",
            "range": "± 736.6476408950866"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12154.945224893505,
            "unit": "ns",
            "range": "± 155.5124977151134"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11058.065209750472,
            "unit": "ns",
            "range": "± 815.8904897293318"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 12353.446513456456,
            "unit": "ns",
            "range": "± 336.9351999649477"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4153817.6958333333,
            "unit": "ns",
            "range": "± 61259.811475814306"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4218603.927490234,
            "unit": "ns",
            "range": "± 83683.4716819699"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5107772.983072917,
            "unit": "ns",
            "range": "± 41686.394444643905"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4890773.430989583,
            "unit": "ns",
            "range": "± 50343.93179794299"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4949665.786979167,
            "unit": "ns",
            "range": "± 67108.04976117385"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7032491.865184295,
            "unit": "ns",
            "range": "± 146987.96308213344"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4906.443726094564,
            "unit": "ns",
            "range": "± 162.43561438435466"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4741.149223327637,
            "unit": "ns",
            "range": "± 12.115993642253937"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4881.884303156535,
            "unit": "ns",
            "range": "± 30.62737252806878"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2210.849412373134,
            "unit": "ns",
            "range": "± 9.459294525451908"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2141.1185522408323,
            "unit": "ns",
            "range": "± 7.116745537785535"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2278.3257562197173,
            "unit": "ns",
            "range": "± 11.22825331986004"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1589675.126325335,
            "unit": "ns",
            "range": "± 3268.606871072529"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1608380.9734375,
            "unit": "ns",
            "range": "± 6201.545367188741"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1588608.062282986,
            "unit": "ns",
            "range": "± 2637.9784568486793"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 826443.9580078125,
            "unit": "ns",
            "range": "± 133639.13197350982"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 677988.5275969328,
            "unit": "ns",
            "range": "± 3483.7435061418187"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 706981.86953125,
            "unit": "ns",
            "range": "± 6623.832124538047"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82022.12841530054,
            "unit": "ns",
            "range": "± 6835.596526832877"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26087.53642384106,
            "unit": "ns",
            "range": "± 1335.4524715108587"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 87390.67015706806,
            "unit": "ns",
            "range": "± 9329.505035164371"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 32504.757653061224,
            "unit": "ns",
            "range": "± 5564.99762399494"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83626.24157303371,
            "unit": "ns",
            "range": "± 6379.731372686508"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 74501.77173913043,
            "unit": "ns",
            "range": "± 5470.982864122757"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 143764.1511627907,
            "unit": "ns",
            "range": "± 7983.675547482452"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 137261.57272727272,
            "unit": "ns",
            "range": "± 6961.727956252244"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 93175.55072463768,
            "unit": "ns",
            "range": "± 10368.969060997299"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83352.3193548387,
            "unit": "ns",
            "range": "± 5499.218259344565"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 93379.47368421052,
            "unit": "ns",
            "range": "± 5255.305796803564"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 90794.375,
            "unit": "ns",
            "range": "± 8295.418012317044"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2361172.1403508773,
            "unit": "ns",
            "range": "± 296109.1044433623"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1704482.1379310344,
            "unit": "ns",
            "range": "± 10308.663550362931"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2058130.7586206896,
            "unit": "ns",
            "range": "± 24216.452573840863"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1712288.3035714286,
            "unit": "ns",
            "range": "± 16825.027732264465"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1982845.05,
            "unit": "ns",
            "range": "± 29813.03990137884"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1988588,
            "unit": "ns",
            "range": "± 21875.87579330569"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1609896.0495867769,
            "unit": "ns",
            "range": "± 70674.23731198558"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1332546.3835616438,
            "unit": "ns",
            "range": "± 63914.488586885054"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1611182.9862068966,
            "unit": "ns",
            "range": "± 75854.90208139211"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1313126.8137254901,
            "unit": "ns",
            "range": "± 39048.95082367269"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1715371.5857988165,
            "unit": "ns",
            "range": "± 174540.65790333043"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1441864.953038674,
            "unit": "ns",
            "range": "± 92776.56363167343"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "2168c823fa3a140a3f5fca81e0db642291fd2cd8",
          "message": "Merge pull request #128 from marius-bughiu/feat/issue-24-string-fnv1a-full-hasher\n\nfeat(hashing): add StringFnV1AFullHasher for full-width string keys",
          "timestamp": "2026-06-02T08:22:25+03:00",
          "tree_id": "94347167e082e66ca7e5aa4f9f66e0fceffe80ad",
          "url": "https://github.com/marius-bughiu/Celerity/commit/2168c823fa3a140a3f5fca81e0db642291fd2cd8"
        },
        "date": 1780379865625,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13722.194580078125,
            "unit": "ns",
            "range": "± 247.53047970435483"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13747.783274057749,
            "unit": "ns",
            "range": "± 323.5962040484619"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14730.709197998047,
            "unit": "ns",
            "range": "± 380.3588236781983"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8638.191074239796,
            "unit": "ns",
            "range": "± 53.92127480905125"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8877.780374461207,
            "unit": "ns",
            "range": "± 59.47791652550699"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9999.809475199381,
            "unit": "ns",
            "range": "± 197.21268882465216"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4234264.419609375,
            "unit": "ns",
            "range": "± 422385.2328678654"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4256497.15625,
            "unit": "ns",
            "range": "± 425300.77975079126"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4169030.4028072036,
            "unit": "ns",
            "range": "± 116058.05073074055"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3235455.7471039873,
            "unit": "ns",
            "range": "± 50835.51740652083"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3232767.165544181,
            "unit": "ns",
            "range": "± 29811.357419221007"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6011818.840494792,
            "unit": "ns",
            "range": "± 71449.99086384493"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4225.743617587619,
            "unit": "ns",
            "range": "± 5.727481115401957"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4016.752519607544,
            "unit": "ns",
            "range": "± 4.518922573488105"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4190.1532273973735,
            "unit": "ns",
            "range": "± 2.770946641292642"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1710.1468828746251,
            "unit": "ns",
            "range": "± 4.537413794450858"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1754.635172843933,
            "unit": "ns",
            "range": "± 7.29640468958326"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1922.3037785750168,
            "unit": "ns",
            "range": "± 1.6554372690063375"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1461840.738389757,
            "unit": "ns",
            "range": "± 4359.829872385453"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1372352.5642801921,
            "unit": "ns",
            "range": "± 57605.529346036834"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1573641.121028646,
            "unit": "ns",
            "range": "± 2949.5990497944726"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 546352.9323814656,
            "unit": "ns",
            "range": "± 2671.5648411945126"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 544358.1526750837,
            "unit": "ns",
            "range": "± 1804.7956260735139"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 678891.8098234953,
            "unit": "ns",
            "range": "± 1826.403326880137"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14660.781977007466,
            "unit": "ns",
            "range": "± 270.4502397105705"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14399.83821596418,
            "unit": "ns",
            "range": "± 229.16517930560246"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 17500.61687783634,
            "unit": "ns",
            "range": "± 505.5295921842584"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9577.272440083821,
            "unit": "ns",
            "range": "± 158.1666643045257"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9217.910902878333,
            "unit": "ns",
            "range": "± 99.06901292773784"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 10130.51351623535,
            "unit": "ns",
            "range": "± 327.8606520091347"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4151873.913993969,
            "unit": "ns",
            "range": "± 122624.59967268091"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4027297.549088542,
            "unit": "ns",
            "range": "± 68813.57280022399"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4985878.771731954,
            "unit": "ns",
            "range": "± 162080.79205275216"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4747355.484263393,
            "unit": "ns",
            "range": "± 90725.72234996648"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4726492.684938525,
            "unit": "ns",
            "range": "± 158901.69714159565"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6534547.707291666,
            "unit": "ns",
            "range": "± 62541.97351390443"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4154.5954548971995,
            "unit": "ns",
            "range": "± 30.969985524130458"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4161.287976495151,
            "unit": "ns",
            "range": "± 9.012499369341674"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4279.561089579264,
            "unit": "ns",
            "range": "± 3.7733801027845737"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2138.442971928914,
            "unit": "ns",
            "range": "± 3.9050425152479145"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2072.881539803964,
            "unit": "ns",
            "range": "± 2.8191542392831144"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2204.3268481663295,
            "unit": "ns",
            "range": "± 3.795996787663141"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1489771.005859375,
            "unit": "ns",
            "range": "± 54585.851718655045"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1536154.0682146992,
            "unit": "ns",
            "range": "± 2307.8409564278813"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1600407.0400390625,
            "unit": "ns",
            "range": "± 2989.2828152966467"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 713360.7367983217,
            "unit": "ns",
            "range": "± 1376.1409707383475"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 681419.2950265066,
            "unit": "ns",
            "range": "± 972.3890337683933"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 861314.2092537716,
            "unit": "ns",
            "range": "± 115042.9607536848"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 72492.56923076924,
            "unit": "ns",
            "range": "± 7337.512651182608"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25252.584507042255,
            "unit": "ns",
            "range": "± 1552.363155047277"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74620.42893401015,
            "unit": "ns",
            "range": "± 9072.696727304301"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25898.579831932773,
            "unit": "ns",
            "range": "± 1243.0044826936796"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 75730.16161616161,
            "unit": "ns",
            "range": "± 8126.255094572928"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 66043.32124352331,
            "unit": "ns",
            "range": "± 5402.681080222896"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 124511.5,
            "unit": "ns",
            "range": "± 8162.771778619477"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 119173.7641025641,
            "unit": "ns",
            "range": "± 7765.206826444973"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 79151.9923076923,
            "unit": "ns",
            "range": "± 7798.320704352331"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 72891.34183673469,
            "unit": "ns",
            "range": "± 7644.366604237678"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 82149.92564102563,
            "unit": "ns",
            "range": "± 7337.147829821799"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 74297.32552083333,
            "unit": "ns",
            "range": "± 7134.837527145894"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1941267.8275862068,
            "unit": "ns",
            "range": "± 35706.98400905323"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1543668.6896551724,
            "unit": "ns",
            "range": "± 8879.6333151184"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1946793.2407407407,
            "unit": "ns",
            "range": "± 29045.84405674349"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1546231.2166666666,
            "unit": "ns",
            "range": "± 14941.924125209101"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2039735.238095238,
            "unit": "ns",
            "range": "± 59216.559665972105"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1944943.1166666667,
            "unit": "ns",
            "range": "± 30062.65283241195"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1679211.89,
            "unit": "ns",
            "range": "± 57360.10301645635"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1325056.512195122,
            "unit": "ns",
            "range": "± 68891.11599719775"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1654516.087628866,
            "unit": "ns",
            "range": "± 50215.182754706715"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1367297.7647058824,
            "unit": "ns",
            "range": "± 25218.747490476475"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1859786.1331360948,
            "unit": "ns",
            "range": "± 131593.98557457072"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1582960.611111111,
            "unit": "ns",
            "range": "± 27362.51449812784"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "fe63e24353dd939e966ab7e9a427089859a1a7cd",
          "message": "Merge pull request #129 from marius-bughiu/feat/issue-24-string-fnv1a-64-hasher\n\nfeat(hashing): add StringFnV1A64Hasher for string keys",
          "timestamp": "2026-06-03T21:34:15+03:00",
          "tree_id": "f7b4425e51fd227b31d01e4ee0aa96ed9b6550e1",
          "url": "https://github.com/marius-bughiu/Celerity/commit/fe63e24353dd939e966ab7e9a427089859a1a7cd"
        },
        "date": 1780513587920,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13442.637594814958,
            "unit": "ns",
            "range": "± 166.69185076904037"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13833.598657016097,
            "unit": "ns",
            "range": "± 255.35008707253292"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13879.298397827148,
            "unit": "ns",
            "range": "± 385.8837688595983"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9343.324091593424,
            "unit": "ns",
            "range": "± 57.832597813845595"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9668.201941030997,
            "unit": "ns",
            "range": "± 62.620919396730116"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10200.207982030408,
            "unit": "ns",
            "range": "± 171.4539609237229"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5171362.140861742,
            "unit": "ns",
            "range": "± 100404.07220701427"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5198357.447265625,
            "unit": "ns",
            "range": "± 73335.84364317925"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5151402.083767361,
            "unit": "ns",
            "range": "± 76504.99582912735"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3518433.8075161637,
            "unit": "ns",
            "range": "± 36982.58585135707"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3589176.52734375,
            "unit": "ns",
            "range": "± 34309.89254985875"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6826832.8223329745,
            "unit": "ns",
            "range": "± 71574.70988416387"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4702.573554710106,
            "unit": "ns",
            "range": "± 4.370713856713008"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4716.323225097656,
            "unit": "ns",
            "range": "± 3.312196497927912"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5057.284625126766,
            "unit": "ns",
            "range": "± 6.97964577929933"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1918.7912470499675,
            "unit": "ns",
            "range": "± 25.79451779457205"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1917.5170378367106,
            "unit": "ns",
            "range": "± 18.316023219782252"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2095.1936218555156,
            "unit": "ns",
            "range": "± 7.510250426016955"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1514726.59375,
            "unit": "ns",
            "range": "± 4437.892208643495"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1454169.8349609375,
            "unit": "ns",
            "range": "± 52419.40782472223"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1603039.611484375,
            "unit": "ns",
            "range": "± 32699.90942900532"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 554096.9354492187,
            "unit": "ns",
            "range": "± 5079.835022402154"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 495072.70669555664,
            "unit": "ns",
            "range": "± 10897.819942946986"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 583500.3975423177,
            "unit": "ns",
            "range": "± 11824.486786808195"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14322.21352814465,
            "unit": "ns",
            "range": "± 349.8280265717129"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14264.740468851725,
            "unit": "ns",
            "range": "± 280.0389481411588"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16291.173656717936,
            "unit": "ns",
            "range": "± 539.9424619713745"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9305.383860996792,
            "unit": "ns",
            "range": "± 400.24953843752917"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8881.814336140951,
            "unit": "ns",
            "range": "± 331.73921131566067"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9440.006159464518,
            "unit": "ns",
            "range": "± 206.99347110807028"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4370940.391241777,
            "unit": "ns",
            "range": "± 93121.32241342378"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4346508.570581896,
            "unit": "ns",
            "range": "± 69419.13589369139"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5329632.096540178,
            "unit": "ns",
            "range": "± 60682.98974716394"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5305968.863002232,
            "unit": "ns",
            "range": "± 85047.79347279148"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5160707.596643519,
            "unit": "ns",
            "range": "± 49209.570398145326"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7635767.796354166,
            "unit": "ns",
            "range": "± 78760.65413876681"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4727.450820377895,
            "unit": "ns",
            "range": "± 8.011102127124984"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4599.207437661978,
            "unit": "ns",
            "range": "± 6.095365891765239"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5042.065590957115,
            "unit": "ns",
            "range": "± 39.36078592732094"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2206.4020675133015,
            "unit": "ns",
            "range": "± 25.845380744316213"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2077.102071762085,
            "unit": "ns",
            "range": "± 10.653406578140903"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2282.9002651487076,
            "unit": "ns",
            "range": "± 4.5039524353041"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1601182.4203404018,
            "unit": "ns",
            "range": "± 8709.020799744023"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1543995.6008463542,
            "unit": "ns",
            "range": "± 6093.328273201685"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1590269.1987680288,
            "unit": "ns",
            "range": "± 2860.8321330305134"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 704587.6375506365,
            "unit": "ns",
            "range": "± 5158.333965707037"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 692125.8110795454,
            "unit": "ns",
            "range": "± 14272.585706879836"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 699261.602505388,
            "unit": "ns",
            "range": "± 5190.220339310808"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80480.63375796178,
            "unit": "ns",
            "range": "± 8186.100225496017"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 31842.894871794873,
            "unit": "ns",
            "range": "± 4125.078837169073"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 77456.69714285714,
            "unit": "ns",
            "range": "± 5483.064891386268"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26332.79508196721,
            "unit": "ns",
            "range": "± 1242.014131276155"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83560.47527472528,
            "unit": "ns",
            "range": "± 6307.975534675952"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 74095.83957219252,
            "unit": "ns",
            "range": "± 5857.867772008823"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 146158.8391304348,
            "unit": "ns",
            "range": "± 8154.94330026107"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 145383.5,
            "unit": "ns",
            "range": "± 2551.0197015815347"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 86991.03886010362,
            "unit": "ns",
            "range": "± 7802.457679701577"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 81182.32989690722,
            "unit": "ns",
            "range": "± 7403.536740386433"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 84977.35975609756,
            "unit": "ns",
            "range": "± 4735.6106322168835"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 74148.65104166667,
            "unit": "ns",
            "range": "± 5289.024544918732"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2300694.017699115,
            "unit": "ns",
            "range": "± 215983.2965364444"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1699653.8793103448,
            "unit": "ns",
            "range": "± 26746.257614936654"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1997224.4166666667,
            "unit": "ns",
            "range": "± 19835.00537932845"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1714348.578125,
            "unit": "ns",
            "range": "± 35328.38608388567"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2001910.81,
            "unit": "ns",
            "range": "± 87769.03528924623"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1938833.423076923,
            "unit": "ns",
            "range": "± 32589.97472864694"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1964958.676966292,
            "unit": "ns",
            "range": "± 249071.21025958844"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1732926.4435897437,
            "unit": "ns",
            "range": "± 149574.18992744177"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6684114.283333333,
            "unit": "ns",
            "range": "± 212168.38643718013"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1507249.8909090909,
            "unit": "ns",
            "range": "± 115899.77837353211"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 2060742.2142857143,
            "unit": "ns",
            "range": "± 292993.5013724789"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1595336.5613496932,
            "unit": "ns",
            "range": "± 88942.92461354657"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "45e040904344d4bf7f3784a9c54b602e5046bdf5",
          "message": "Merge pull request #130 from marius-bughiu/feat/issue-24-string-xxhash32-hasher\n\nfeat(hashing): add StringXxHash32Hasher for string keys",
          "timestamp": "2026-06-03T21:45:55+03:00",
          "tree_id": "59459e70be43b3db623159b88de9a469b440e9bc",
          "url": "https://github.com/marius-bughiu/Celerity/commit/45e040904344d4bf7f3784a9c54b602e5046bdf5"
        },
        "date": 1780514429871,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12021.955563100179,
            "unit": "ns",
            "range": "± 102.85636458371718"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13234.130002913937,
            "unit": "ns",
            "range": "± 286.59076031697685"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13215.037979125977,
            "unit": "ns",
            "range": "± 307.5039829947392"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8825.01911468506,
            "unit": "ns",
            "range": "± 143.81810064548208"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8801.219567660628,
            "unit": "ns",
            "range": "± 74.27854906123123"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9081.121230191962,
            "unit": "ns",
            "range": "± 408.70964325258973"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5082465.408203125,
            "unit": "ns",
            "range": "± 125384.60033478473"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5034567.637369792,
            "unit": "ns",
            "range": "± 128060.71240686349"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4510283.67061942,
            "unit": "ns",
            "range": "± 200308.09184288513"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3172008.099676724,
            "unit": "ns",
            "range": "± 20195.66459839661"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3177219.7900390625,
            "unit": "ns",
            "range": "± 29027.198743818473"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6072160.588900862,
            "unit": "ns",
            "range": "± 39729.38383439497"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4750.133924484253,
            "unit": "ns",
            "range": "± 23.17881537988841"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4737.819208498354,
            "unit": "ns",
            "range": "± 8.0233905272457"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4744.621678387677,
            "unit": "ns",
            "range": "± 5.666949472738896"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1862.2538333761281,
            "unit": "ns",
            "range": "± 8.97429478437243"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1799.726418462293,
            "unit": "ns",
            "range": "± 6.2493796253046385"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2010.2997583459926,
            "unit": "ns",
            "range": "± 6.978610066328574"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1558978.6258755387,
            "unit": "ns",
            "range": "± 11504.68955053444"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1548827.085078125,
            "unit": "ns",
            "range": "± 7585.55866083409"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1604387.0541735198,
            "unit": "ns",
            "range": "± 41821.132252578216"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 590525.6723813658,
            "unit": "ns",
            "range": "± 2687.9810030615645"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 574027.1666992188,
            "unit": "ns",
            "range": "± 2441.3773448118773"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 638719.1053641184,
            "unit": "ns",
            "range": "± 1751.0808654332784"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14307.426656169277,
            "unit": "ns",
            "range": "± 1390.1104778499634"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 12991.060075957199,
            "unit": "ns",
            "range": "± 322.4671930148969"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14254.770194218076,
            "unit": "ns",
            "range": "± 104.67599591297456"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11773.168526240757,
            "unit": "ns",
            "range": "± 122.25028256546662"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11011.009499104817,
            "unit": "ns",
            "range": "± 73.4066628818926"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 10967.24331665039,
            "unit": "ns",
            "range": "± 51.59192593404406"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4128650.6047794116,
            "unit": "ns",
            "range": "± 79280.19389716157"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4090657.22265625,
            "unit": "ns",
            "range": "± 81042.74336300859"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4938457.716666667,
            "unit": "ns",
            "range": "± 65405.93381176138"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4746163.315885416,
            "unit": "ns",
            "range": "± 44442.861255996824"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4816770.104121767,
            "unit": "ns",
            "range": "± 47673.35448806346"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6837753.099414063,
            "unit": "ns",
            "range": "± 163600.03560926998"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4749.194710040914,
            "unit": "ns",
            "range": "± 23.477454302250685"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4731.72954423087,
            "unit": "ns",
            "range": "± 14.126508275333848"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4884.179423195975,
            "unit": "ns",
            "range": "± 28.496878286978035"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 5619.8736782073975,
            "unit": "ns",
            "range": "± 3732.543297516083"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2143.1640075683595,
            "unit": "ns",
            "range": "± 3.6813471454841324"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2280.9913234710693,
            "unit": "ns",
            "range": "± 15.733434783728622"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1587856.0745804398,
            "unit": "ns",
            "range": "± 2900.8543390347745"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1599350.5336408943,
            "unit": "ns",
            "range": "± 3879.5120565093757"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1587121.6896033655,
            "unit": "ns",
            "range": "± 7670.900721131601"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 718787.4557128906,
            "unit": "ns",
            "range": "± 19249.164782973083"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 671337.9621175131,
            "unit": "ns",
            "range": "± 1895.7287272083188"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 694725.5752301897,
            "unit": "ns",
            "range": "± 2007.372034388627"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81040.94535519126,
            "unit": "ns",
            "range": "± 7028.5283171293995"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26183.182926829268,
            "unit": "ns",
            "range": "± 1417.959214176054"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 84995.44382022473,
            "unit": "ns",
            "range": "± 6292.501059097796"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 31196.914772727272,
            "unit": "ns",
            "range": "± 4685.424620319358"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 84110.4,
            "unit": "ns",
            "range": "± 6617.546962925292"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 76377.29370629371,
            "unit": "ns",
            "range": "± 5233.302293871873"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 145273.63157894736,
            "unit": "ns",
            "range": "± 3401.912198138082"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 138304.49,
            "unit": "ns",
            "range": "± 4468.439025132437"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 92688.96571428572,
            "unit": "ns",
            "range": "± 7221.315443693307"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 87460.208994709,
            "unit": "ns",
            "range": "± 6329.03873032642"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 90044.30821917808,
            "unit": "ns",
            "range": "± 2984.2947515448564"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 80755.56349206349,
            "unit": "ns",
            "range": "± 4275.269242975991"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2064992.0333333334,
            "unit": "ns",
            "range": "± 32222.671084601858"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1724394.35,
            "unit": "ns",
            "range": "± 14863.20579087488"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2050424.0344827587,
            "unit": "ns",
            "range": "± 26980.68074366593"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1712059.0833333333,
            "unit": "ns",
            "range": "± 18120.645005972365"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1985533.8,
            "unit": "ns",
            "range": "± 21982.40369869532"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1978258.7166666666,
            "unit": "ns",
            "range": "± 10825.607313284407"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1579746.322580645,
            "unit": "ns",
            "range": "± 22026.209603020743"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1304710.1355140186,
            "unit": "ns",
            "range": "± 54248.05666228758"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2513473.625,
            "unit": "ns",
            "range": "± 1998992.1757475226"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1334908.3793103448,
            "unit": "ns",
            "range": "± 17218.583381695888"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 6847494.966666667,
            "unit": "ns",
            "range": "± 83150.63401358356"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1417289.9464285714,
            "unit": "ns",
            "range": "± 18230.73270623765"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7a2ca5953adcc55931fb58391b02c0a760512489",
          "message": "Merge pull request #131 from marius-bughiu/feat/issue-24-string-xxhash64-hasher\n\nfeat(hashing): add StringXxHash64Hasher for string keys",
          "timestamp": "2026-06-03T22:04:08+03:00",
          "tree_id": "9872d9e69e48322d574c841c2cb7a6ec566f2644",
          "url": "https://github.com/marius-bughiu/Celerity/commit/7a2ca5953adcc55931fb58391b02c0a760512489"
        },
        "date": 1780515531806,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12615.280579703194,
            "unit": "ns",
            "range": "± 149.1803830636166"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12759.045195922852,
            "unit": "ns",
            "range": "± 99.90968274386393"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14014.227215796402,
            "unit": "ns",
            "range": "± 651.7899134594771"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9270.398210313586,
            "unit": "ns",
            "range": "± 214.54810680521723"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9568.840730455187,
            "unit": "ns",
            "range": "± 204.9928152810351"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9924.72876180013,
            "unit": "ns",
            "range": "± 291.6078783626726"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5297585.075683594,
            "unit": "ns",
            "range": "± 114458.78292450846"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5313289.766755757,
            "unit": "ns",
            "range": "± 121601.78695210245"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5150919.401646205,
            "unit": "ns",
            "range": "± 100717.05147007508"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3475841.3147898708,
            "unit": "ns",
            "range": "± 15239.60997846736"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3563081.531705729,
            "unit": "ns",
            "range": "± 28458.36723361621"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6892668.569270833,
            "unit": "ns",
            "range": "± 56457.29657948262"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4711.722302300589,
            "unit": "ns",
            "range": "± 9.903889462162686"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4704.522092672495,
            "unit": "ns",
            "range": "± 4.410235004004622"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5086.863945572464,
            "unit": "ns",
            "range": "± 25.210063398301504"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1916.578059887064,
            "unit": "ns",
            "range": "± 25.19362850930652"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1913.9119000928156,
            "unit": "ns",
            "range": "± 16.285571958517473"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2093.090751375471,
            "unit": "ns",
            "range": "± 4.610676008930091"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1441092.4958814539,
            "unit": "ns",
            "range": "± 51432.21520753181"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1513143.36265346,
            "unit": "ns",
            "range": "± 3440.819574205542"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1550700.960973669,
            "unit": "ns",
            "range": "± 76598.99369436543"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 551522.4997979526,
            "unit": "ns",
            "range": "± 3012.2227708932046"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 496808.4146384215,
            "unit": "ns",
            "range": "± 10448.923789278595"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 583844.6684727822,
            "unit": "ns",
            "range": "± 10614.91282378931"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13785.817979079027,
            "unit": "ns",
            "range": "± 376.2427110938239"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13961.032357418177,
            "unit": "ns",
            "range": "± 325.22298671753356"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15623.759881851522,
            "unit": "ns",
            "range": "± 403.6343491853014"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9319.030101639884,
            "unit": "ns",
            "range": "± 385.0529773924842"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9016.7567756176,
            "unit": "ns",
            "range": "± 357.4424985833716"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9204.71892227665,
            "unit": "ns",
            "range": "± 184.427849080335"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4381844.5234375,
            "unit": "ns",
            "range": "± 83336.68901207144"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4417878.703613281,
            "unit": "ns",
            "range": "± 106043.55029503632"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5277720.764008621,
            "unit": "ns",
            "range": "± 40905.304752793345"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5153043.186067709,
            "unit": "ns",
            "range": "± 85092.06726419092"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5194690.594019396,
            "unit": "ns",
            "range": "± 47946.365773638776"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7669559.257543104,
            "unit": "ns",
            "range": "± 61545.5871468006"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4872.741584212692,
            "unit": "ns",
            "range": "± 153.0999642272413"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4604.65421266909,
            "unit": "ns",
            "range": "± 7.7041557068391935"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5032.4066317240395,
            "unit": "ns",
            "range": "± 38.325260013317305"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2213.2553788546857,
            "unit": "ns",
            "range": "± 10.909313399601302"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2079.911931037903,
            "unit": "ns",
            "range": "± 5.349158707555456"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2282.907028480812,
            "unit": "ns",
            "range": "± 4.420743694777209"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1614392.7790948276,
            "unit": "ns",
            "range": "± 5933.266207116114"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1545051.4267926898,
            "unit": "ns",
            "range": "± 5131.033332079222"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1648610.0798688617,
            "unit": "ns",
            "range": "± 39501.08475617325"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 703832.2170973558,
            "unit": "ns",
            "range": "± 14537.76534828211"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 692288.907890625,
            "unit": "ns",
            "range": "± 7923.961505820054"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 754384.8760279606,
            "unit": "ns",
            "range": "± 45361.43908043212"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 78291.99470899471,
            "unit": "ns",
            "range": "± 6859.740110840186"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27228.6884057971,
            "unit": "ns",
            "range": "± 1802.0097369274622"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79693.73684210527,
            "unit": "ns",
            "range": "± 7584.198836652001"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29528.617486338797,
            "unit": "ns",
            "range": "± 3208.655853873027"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82679.39583333333,
            "unit": "ns",
            "range": "± 7807.384953097065"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 73877.26190476191,
            "unit": "ns",
            "range": "± 6385.905171949148"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 152882.7879581152,
            "unit": "ns",
            "range": "± 13327.906811246665"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 140794.3015873016,
            "unit": "ns",
            "range": "± 7625.764623191398"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 91012.55789473685,
            "unit": "ns",
            "range": "± 7586.843432141344"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 82810.37305699482,
            "unit": "ns",
            "range": "± 8312.576039483005"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 82794.28608247422,
            "unit": "ns",
            "range": "± 6428.718492460028"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 75555.88541666667,
            "unit": "ns",
            "range": "± 6688.382888426362"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2007639.1785714286,
            "unit": "ns",
            "range": "± 13324.997475654185"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1697824.625,
            "unit": "ns",
            "range": "± 34830.91366329116"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2013927.5689655172,
            "unit": "ns",
            "range": "± 29441.6924575156"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1769465.2352941176,
            "unit": "ns",
            "range": "± 65001.22114733925"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2006790.5869565217,
            "unit": "ns",
            "range": "± 50207.612337208215"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1951064.0689655172,
            "unit": "ns",
            "range": "± 25633.919516894897"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1763998.9895833333,
            "unit": "ns",
            "range": "± 67377.11266485469"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1391722.0722891567,
            "unit": "ns",
            "range": "± 24378.218294498536"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6488224.931034483,
            "unit": "ns",
            "range": "± 98842.48158241513"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1445604.4112149533,
            "unit": "ns",
            "range": "± 59333.811860722766"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1931913.9759036144,
            "unit": "ns",
            "range": "± 181963.1838514427"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1560240.076923077,
            "unit": "ns",
            "range": "± 55342.33145332154"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6c4278d1c9660868062b82071fec3b89e6594d79",
          "message": "Merge pull request #132 from marius-bughiu/feat/issue-24-string-metrohash64-hasher\n\nfeat(hashing): add StringMetroHash64Hasher for string keys",
          "timestamp": "2026-06-03T22:23:03+03:00",
          "tree_id": "c98b157adccc1694030580a2d120deb3d9c1b864",
          "url": "https://github.com/marius-bughiu/Celerity/commit/6c4278d1c9660868062b82071fec3b89e6594d79"
        },
        "date": 1780516540682,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13196.689819784726,
            "unit": "ns",
            "range": "± 274.97942396604446"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12938.459220690605,
            "unit": "ns",
            "range": "± 356.1470152583202"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13606.434943135579,
            "unit": "ns",
            "range": "± 232.61012288587392"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9425.924886769262,
            "unit": "ns",
            "range": "± 131.31618854968704"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9498.84632768302,
            "unit": "ns",
            "range": "± 126.13791728988735"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10338.36798655192,
            "unit": "ns",
            "range": "± 195.90223006582363"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5211932.049479167,
            "unit": "ns",
            "range": "± 82291.9181214787"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5215612.423564189,
            "unit": "ns",
            "range": "± 107789.13234985992"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5170295.1761067705,
            "unit": "ns",
            "range": "± 102121.99017380898"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3478019.271955819,
            "unit": "ns",
            "range": "± 25285.08495591494"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3545068.3099407325,
            "unit": "ns",
            "range": "± 49409.028481931775"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6791594.706510416,
            "unit": "ns",
            "range": "± 85034.46537338036"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4727.269242180719,
            "unit": "ns",
            "range": "± 10.59057679247672"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4710.562777201335,
            "unit": "ns",
            "range": "± 10.30595490438259"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5064.424008789062,
            "unit": "ns",
            "range": "± 5.636678663335393"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1929.9589336658346,
            "unit": "ns",
            "range": "± 18.123519969909438"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1917.963484139278,
            "unit": "ns",
            "range": "± 18.339498920387758"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2091.379090344464,
            "unit": "ns",
            "range": "± 4.153921265845588"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1539357.9630353008,
            "unit": "ns",
            "range": "± 23478.484935150565"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1460949.9706280048,
            "unit": "ns",
            "range": "± 49148.39117121985"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1693245.3560474536,
            "unit": "ns",
            "range": "± 120090.40158776086"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 549692.4246303013,
            "unit": "ns",
            "range": "± 2835.3359478232906"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 484650.31117466517,
            "unit": "ns",
            "range": "± 6360.302861804458"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 586210.7434997559,
            "unit": "ns",
            "range": "± 11590.027135128354"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14334.388422766397,
            "unit": "ns",
            "range": "± 352.63439044786554"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13703.340264608694,
            "unit": "ns",
            "range": "± 346.63014275302464"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15686.652347433155,
            "unit": "ns",
            "range": "± 345.6683253193047"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9280.291044564083,
            "unit": "ns",
            "range": "± 206.82225068930381"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8677.135685654937,
            "unit": "ns",
            "range": "± 288.94155096818866"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9429.109908073178,
            "unit": "ns",
            "range": "± 133.1167360680441"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4407247.459114583,
            "unit": "ns",
            "range": "± 82142.98443168412"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4419993.221484375,
            "unit": "ns",
            "range": "± 60632.90039331097"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5321639.75309806,
            "unit": "ns",
            "range": "± 73153.20792115606"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5201488.9278017245,
            "unit": "ns",
            "range": "± 89905.89746127908"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5201630.310075431,
            "unit": "ns",
            "range": "± 65405.478155441764"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7709997.511574074,
            "unit": "ns",
            "range": "± 49786.42802218893"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4719.853222317166,
            "unit": "ns",
            "range": "± 11.63945341417546"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4729.578242081862,
            "unit": "ns",
            "range": "± 135.3081672743523"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5033.461219787598,
            "unit": "ns",
            "range": "± 34.44190940406632"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2206.041196893763,
            "unit": "ns",
            "range": "± 19.945683032628462"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2084.085359639135,
            "unit": "ns",
            "range": "± 8.317382206869002"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2283.977673457219,
            "unit": "ns",
            "range": "± 6.136784700729311"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1600490.375,
            "unit": "ns",
            "range": "± 11908.381606047753"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1577813.40484375,
            "unit": "ns",
            "range": "± 28610.486022056728"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1609104.3260216345,
            "unit": "ns",
            "range": "± 18494.42459385122"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 740106.1408617424,
            "unit": "ns",
            "range": "± 39220.02625369875"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 770078.9994303385,
            "unit": "ns",
            "range": "± 15464.528635065253"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 723531.6836799172,
            "unit": "ns",
            "range": "± 36683.17805214199"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80660.41411042945,
            "unit": "ns",
            "range": "± 7295.113600854174"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26047.765625,
            "unit": "ns",
            "range": "± 1960.4174660300896"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 76571.6178343949,
            "unit": "ns",
            "range": "± 5207.175584802375"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30583.05909090909,
            "unit": "ns",
            "range": "± 3096.4704335030874"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81478.66326530612,
            "unit": "ns",
            "range": "± 8110.457065363524"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70021.13243243244,
            "unit": "ns",
            "range": "± 5455.224128124604"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 148239.9655172414,
            "unit": "ns",
            "range": "± 10166.401996115766"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 140636.75661375662,
            "unit": "ns",
            "range": "± 10525.645576502668"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 90661.64095744681,
            "unit": "ns",
            "range": "± 7623.274482769884"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 80899.60824742269,
            "unit": "ns",
            "range": "± 7556.083922898146"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 84826.71942446043,
            "unit": "ns",
            "range": "± 5085.672757042219"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 77296.45360824742,
            "unit": "ns",
            "range": "± 8094.281878103142"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2008775.4016393442,
            "unit": "ns",
            "range": "± 68857.70853614355"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1677045.3392857143,
            "unit": "ns",
            "range": "± 17026.004632377288"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2003719.3970588236,
            "unit": "ns",
            "range": "± 33066.95039287017"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1683332.9833333334,
            "unit": "ns",
            "range": "± 20806.91576238093"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1988316.2571428572,
            "unit": "ns",
            "range": "± 40931.11404370034"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1945109.1166666667,
            "unit": "ns",
            "range": "± 15711.134160142245"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1678534.5,
            "unit": "ns",
            "range": "± 22282.532046425964"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1410042.7962962964,
            "unit": "ns",
            "range": "± 67606.67110082466"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6400162.615384615,
            "unit": "ns",
            "range": "± 76770.95157210279"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1448602.857142857,
            "unit": "ns",
            "range": "± 22020.445433504454"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1799820.5186567164,
            "unit": "ns",
            "range": "± 81064.73820129433"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1573498.5172413792,
            "unit": "ns",
            "range": "± 21837.039537414883"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "650b01c9684031240f7f41a5fa36214afcdf41c6",
          "message": "Merge pull request #133 from marius-bughiu/feat/issue-24-string-cityhash64-hasher\n\nfeat(hashing): add StringCityHash64Hasher for string keys",
          "timestamp": "2026-06-03T22:42:42+03:00",
          "tree_id": "0e3f7bf1a9fd56a3a30447a03030af69c7580eb8",
          "url": "https://github.com/marius-bughiu/Celerity/commit/650b01c9684031240f7f41a5fa36214afcdf41c6"
        },
        "date": 1780517771232,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12691.179196421306,
            "unit": "ns",
            "range": "± 207.6180389489852"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12485.652185374292,
            "unit": "ns",
            "range": "± 65.31231381723343"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13626.911431230817,
            "unit": "ns",
            "range": "± 291.8160662471101"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9102.784314385775,
            "unit": "ns",
            "range": "± 89.4805150421884"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9336.420862325032,
            "unit": "ns",
            "range": "± 126.21679322164849"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9816.863763217269,
            "unit": "ns",
            "range": "± 140.88323314655247"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5202013.213496767,
            "unit": "ns",
            "range": "± 86039.7262896826"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5233446.560791016,
            "unit": "ns",
            "range": "± 96536.23582854967"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5062287.724724265,
            "unit": "ns",
            "range": "± 105370.9136905086"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3502914.650251116,
            "unit": "ns",
            "range": "± 25875.852085664505"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3538755.1669921875,
            "unit": "ns",
            "range": "± 19065.91075233357"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6760263.7265625,
            "unit": "ns",
            "range": "± 114820.65266459492"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4712.949559138371,
            "unit": "ns",
            "range": "± 6.339201168136256"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4706.493710632324,
            "unit": "ns",
            "range": "± 8.802811190132031"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5446.675351743345,
            "unit": "ns",
            "range": "± 344.67666131710337"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1927.473277190636,
            "unit": "ns",
            "range": "± 23.951632224858468"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1912.3780100005013,
            "unit": "ns",
            "range": "± 13.21127392196966"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2090.7057483814383,
            "unit": "ns",
            "range": "± 7.7801346358314465"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1512944.0989583333,
            "unit": "ns",
            "range": "± 3166.72794317356"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1502874.4327980324,
            "unit": "ns",
            "range": "± 1993.2468692205405"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1597355.2154947917,
            "unit": "ns",
            "range": "± 22641.113976515786"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 551783.6028180803,
            "unit": "ns",
            "range": "± 2968.4207349623507"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 493797.4750325521,
            "unit": "ns",
            "range": "± 10880.689804562733"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 579684.1060872396,
            "unit": "ns",
            "range": "± 9902.93417197492"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13663.385691129244,
            "unit": "ns",
            "range": "± 324.477905994962"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13997.804517022494,
            "unit": "ns",
            "range": "± 247.18789451406573"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15849.922747802735,
            "unit": "ns",
            "range": "± 481.0404388526011"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9034.07820551019,
            "unit": "ns",
            "range": "± 241.961583490434"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9121.125605532998,
            "unit": "ns",
            "range": "± 368.311184894371"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9279.250619615827,
            "unit": "ns",
            "range": "± 261.3895383842391"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4359941.166852678,
            "unit": "ns",
            "range": "± 66454.73243795785"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4370399.369280134,
            "unit": "ns",
            "range": "± 81489.2150071795"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5293541.217672414,
            "unit": "ns",
            "range": "± 57291.101780797566"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5180896.213411459,
            "unit": "ns",
            "range": "± 113841.14035050866"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5336767.149274553,
            "unit": "ns",
            "range": "± 53566.03576651503"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7606147.091435186,
            "unit": "ns",
            "range": "± 84748.47235334446"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4720.224034909849,
            "unit": "ns",
            "range": "± 7.170089752753926"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4605.803069188045,
            "unit": "ns",
            "range": "± 10.484799529171537"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5034.551353717672,
            "unit": "ns",
            "range": "± 37.15327727944462"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2206.2296659535377,
            "unit": "ns",
            "range": "± 20.025704632592042"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2099.7843050073693,
            "unit": "ns",
            "range": "± 10.694646619228045"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2283.201976776123,
            "unit": "ns",
            "range": "± 7.277118565767551"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1603530.663155692,
            "unit": "ns",
            "range": "± 8208.450886725144"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1570005.9404672475,
            "unit": "ns",
            "range": "± 28542.568623601615"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1597409.8030960648,
            "unit": "ns",
            "range": "± 3692.8648116378326"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 728290.7178470141,
            "unit": "ns",
            "range": "± 31395.47585320716"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 717482.7176695479,
            "unit": "ns",
            "range": "± 38789.21196623618"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 701811.9074454472,
            "unit": "ns",
            "range": "± 28076.272873903607"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82655.038071066,
            "unit": "ns",
            "range": "± 9936.863257347706"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28814.094736842104,
            "unit": "ns",
            "range": "± 3717.588310735914"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 78994.91237113402,
            "unit": "ns",
            "range": "± 8730.370207271395"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26486.6546961326,
            "unit": "ns",
            "range": "± 1793.8518777394884"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81650.3835978836,
            "unit": "ns",
            "range": "± 6159.463706897804"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 72679.68648648649,
            "unit": "ns",
            "range": "± 6423.578047613634"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 156427.0357142857,
            "unit": "ns",
            "range": "± 3715.393416113327"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 146728.1611570248,
            "unit": "ns",
            "range": "± 10013.839473909527"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 87527.46683673469,
            "unit": "ns",
            "range": "± 7131.698560592067"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 81244.49740932643,
            "unit": "ns",
            "range": "± 6664.068851305574"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 82770.50777202072,
            "unit": "ns",
            "range": "± 6924.367634423958"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 73976.00793650794,
            "unit": "ns",
            "range": "± 4994.555877236827"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2013757.3333333333,
            "unit": "ns",
            "range": "± 29979.557513623244"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1672445.7166666666,
            "unit": "ns",
            "range": "± 15014.090299749198"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2015383.7586206896,
            "unit": "ns",
            "range": "± 24173.610164887265"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1673625.9137931035,
            "unit": "ns",
            "range": "± 16615.640158484162"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2021805.8636363635,
            "unit": "ns",
            "range": "± 49068.06282401147"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1945990.2666666666,
            "unit": "ns",
            "range": "± 31132.83938216048"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1854501.2846153846,
            "unit": "ns",
            "range": "± 119868.93453825712"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1432089.85,
            "unit": "ns",
            "range": "± 61269.96887624281"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 3097325.105769231,
            "unit": "ns",
            "range": "± 2210601.0974571127"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1431763.2,
            "unit": "ns",
            "range": "± 12643.006992145829"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 2597899.986842105,
            "unit": "ns",
            "range": "± 1771855.975885078"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1578298.1923076923,
            "unit": "ns",
            "range": "± 87804.37822045723"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "67d993f4de3546ecd61acce075a604391f5c069d",
          "message": "Merge pull request #134 from marius-bughiu/feat/issue-24-string-siphash24-hasher\n\nfeat(hashing): add StringSipHash24Hasher for string keys",
          "timestamp": "2026-06-03T22:57:09+03:00",
          "tree_id": "850134b34a3d68208ad294324136a1821d539cbb",
          "url": "https://github.com/marius-bughiu/Celerity/commit/67d993f4de3546ecd61acce075a604391f5c069d"
        },
        "date": 1780519175359,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 10595.106616289188,
            "unit": "ns",
            "range": "± 297.6288160909072"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 10406.143155226067,
            "unit": "ns",
            "range": "± 401.7879112347498"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 11512.026169069351,
            "unit": "ns",
            "range": "± 433.8881420125282"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 7864.156593127128,
            "unit": "ns",
            "range": "± 269.08725954155864"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 7459.541406685966,
            "unit": "ns",
            "range": "± 141.5256744635681"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 7727.370729234483,
            "unit": "ns",
            "range": "± 271.3301378720939"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4351784.602864583,
            "unit": "ns",
            "range": "± 107554.85529830528"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4334638.50080819,
            "unit": "ns",
            "range": "± 57330.93054010048"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 3901568.75771545,
            "unit": "ns",
            "range": "± 204080.67653132303"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 2761197.92421875,
            "unit": "ns",
            "range": "± 44003.380590353"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 2782064.1929408484,
            "unit": "ns",
            "range": "± 18581.86847283002"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5446611.00234375,
            "unit": "ns",
            "range": "± 64345.76817188725"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3759.3121078197773,
            "unit": "ns",
            "range": "± 119.99974872053954"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3656.830361879789,
            "unit": "ns",
            "range": "± 3.1823678401102335"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 3955.772205080305,
            "unit": "ns",
            "range": "± 4.0657613430780355"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1491.0003565470377,
            "unit": "ns",
            "range": "± 15.926090422637706"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1490.7486237843832,
            "unit": "ns",
            "range": "± 19.586874830431984"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1625.1699578211858,
            "unit": "ns",
            "range": "± 2.3101709585359282"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1221956.7998408566,
            "unit": "ns",
            "range": "± 42368.57245397614"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1218051.7167217548,
            "unit": "ns",
            "range": "± 39410.2370082541"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1224952.1573392428,
            "unit": "ns",
            "range": "± 2448.6005290589815"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 395297.13325557,
            "unit": "ns",
            "range": "± 27768.69444922228"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 376971.8812561035,
            "unit": "ns",
            "range": "± 7587.8361315152115"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 482130.5684273635,
            "unit": "ns",
            "range": "± 17056.44158762324"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 11114.472363910978,
            "unit": "ns",
            "range": "± 362.1511951791709"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 11062.832786378407,
            "unit": "ns",
            "range": "± 448.32208345480507"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13086.674421647016,
            "unit": "ns",
            "range": "± 443.8732806728804"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 7125.192427431304,
            "unit": "ns",
            "range": "± 366.0032871489492"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 7058.426129074097,
            "unit": "ns",
            "range": "± 479.5261532541605"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 7475.980664429841,
            "unit": "ns",
            "range": "± 214.06332771589956"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3456492.1796875,
            "unit": "ns",
            "range": "± 59355.31680309442"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3448465.29765625,
            "unit": "ns",
            "range": "± 56643.99129916033"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4372917.934194712,
            "unit": "ns",
            "range": "± 123864.68973753812"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4235342.071533203,
            "unit": "ns",
            "range": "± 71630.96978699717"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4185996.7003348214,
            "unit": "ns",
            "range": "± 40515.55207689896"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6284040.639008621,
            "unit": "ns",
            "range": "± 104196.61608837043"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3662.635411922748,
            "unit": "ns",
            "range": "± 6.016115355854216"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3659.4284173420497,
            "unit": "ns",
            "range": "± 90.95110149840019"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 3866.040862471969,
            "unit": "ns",
            "range": "± 2.8044218512778487"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 1711.5630546297346,
            "unit": "ns",
            "range": "± 7.110001611472392"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 1737.045438700709,
            "unit": "ns",
            "range": "± 118.39419206525533"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 1725.4825117323135,
            "unit": "ns",
            "range": "± 3.3245251616438405"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1227185.908135776,
            "unit": "ns",
            "range": "± 18585.452446898402"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1178943.826239224,
            "unit": "ns",
            "range": "± 39112.110188472674"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1247318.4756944445,
            "unit": "ns",
            "range": "± 4845.6056581675875"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 534157.4609695184,
            "unit": "ns",
            "range": "± 38629.979835195605"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 442310.7464304957,
            "unit": "ns",
            "range": "± 9979.229322313513"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 571008.4793031754,
            "unit": "ns",
            "range": "± 32642.11802079179"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 60383.62051282051,
            "unit": "ns",
            "range": "± 5948.995209241394"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 22542.994897959183,
            "unit": "ns",
            "range": "± 3403.9484034437373"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 62807.851851851854,
            "unit": "ns",
            "range": "± 4913.127781874054"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 22606.39285714286,
            "unit": "ns",
            "range": "± 3008.243940215977"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 63955.48601398602,
            "unit": "ns",
            "range": "± 6028.186690119159"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 55730.12886597938,
            "unit": "ns",
            "range": "± 5424.991258453046"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 114805.91304347826,
            "unit": "ns",
            "range": "± 7215.3220071866035"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 110269.06435643564,
            "unit": "ns",
            "range": "± 5966.259781749101"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 69885.0621761658,
            "unit": "ns",
            "range": "± 5748.823127413181"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 64755.28795811519,
            "unit": "ns",
            "range": "± 4987.383613290377"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 65025.963917525776,
            "unit": "ns",
            "range": "± 6261.999197291377"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 58676.80829015544,
            "unit": "ns",
            "range": "± 4806.277355745046"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1553878.6785714286,
            "unit": "ns",
            "range": "± 16049.417338110972"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1303803.5666666667,
            "unit": "ns",
            "range": "± 14263.0498375283"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1562549.6206896552,
            "unit": "ns",
            "range": "± 15353.150791482027"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1303456.7068965517,
            "unit": "ns",
            "range": "± 12976.074014534019"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1534785.5675675676,
            "unit": "ns",
            "range": "± 32403.69429730757"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1651716.0086956522,
            "unit": "ns",
            "range": "± 175414.64568078614"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 3154411.0859375,
            "unit": "ns",
            "range": "± 3310583.94992326"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1054130.1103896103,
            "unit": "ns",
            "range": "± 9476.809585781166"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1459092.0386904762,
            "unit": "ns",
            "range": "± 107807.96182168875"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1140303.1911764706,
            "unit": "ns",
            "range": "± 24945.527289328784"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1421252.4847560977,
            "unit": "ns",
            "range": "± 82040.72348040587"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1237305.3439490446,
            "unit": "ns",
            "range": "± 67880.49243686056"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "58a1f7f3de4a2334d100e046ecbe9dacd493f60e",
          "message": "Merge pull request #135 from marius-bughiu/feat/issue-24-string-highwayhash64-hasher\n\nfeat(hashing): add StringHighwayHash64Hasher for string keys",
          "timestamp": "2026-06-03T23:15:34+03:00",
          "tree_id": "41e9ae69c909389e88712d38f9b7a0b2bc37eebf",
          "url": "https://github.com/marius-bughiu/Celerity/commit/58a1f7f3de4a2334d100e046ecbe9dacd493f60e"
        },
        "date": 1780519692952,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12161.820411137172,
            "unit": "ns",
            "range": "± 93.5201550423168"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12203.510682373048,
            "unit": "ns",
            "range": "± 222.1641613063751"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12947.862502525593,
            "unit": "ns",
            "range": "± 372.7255289018232"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8588.289159502301,
            "unit": "ns",
            "range": "± 27.875364053055076"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8576.996190752301,
            "unit": "ns",
            "range": "± 134.95423880636625"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9093.975203941609,
            "unit": "ns",
            "range": "± 25.780911037317104"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4938610.840066965,
            "unit": "ns",
            "range": "± 93025.0132964083"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4939323.547395834,
            "unit": "ns",
            "range": "± 96516.07505638569"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4324806.791927083,
            "unit": "ns",
            "range": "± 79101.62797830011"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3163452.8022135417,
            "unit": "ns",
            "range": "± 22356.686655520396"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3152965.0751953125,
            "unit": "ns",
            "range": "± 15004.304071674414"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5988707.839322916,
            "unit": "ns",
            "range": "± 42102.94418464616"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4753.338797302246,
            "unit": "ns",
            "range": "± 30.033996028330634"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4780.546537252573,
            "unit": "ns",
            "range": "± 49.60100830679921"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4893.166024071829,
            "unit": "ns",
            "range": "± 10.221870981105145"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1862.4287007876806,
            "unit": "ns",
            "range": "± 6.140566595346983"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1801.2546764373778,
            "unit": "ns",
            "range": "± 5.394320613170837"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2000.0734299879807,
            "unit": "ns",
            "range": "± 7.06758059355134"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1567688.142505787,
            "unit": "ns",
            "range": "± 17079.10318359019"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1551019.044994213,
            "unit": "ns",
            "range": "± 2039.1495199140816"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1631740.4813058036,
            "unit": "ns",
            "range": "± 8505.077255571188"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 588102.1043795072,
            "unit": "ns",
            "range": "± 1376.2182737716475"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 571089.3608230065,
            "unit": "ns",
            "range": "± 1958.381103676808"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 639377.610152633,
            "unit": "ns",
            "range": "± 310.4847751069919"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14640.086366990034,
            "unit": "ns",
            "range": "± 1186.2631587703129"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14707.097492086476,
            "unit": "ns",
            "range": "± 993.1357147477013"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15845.091351677389,
            "unit": "ns",
            "range": "± 320.718078781105"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12007.884438717005,
            "unit": "ns",
            "range": "± 202.0655395014394"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11703.927453139733,
            "unit": "ns",
            "range": "± 85.19052612082267"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9932.764449387454,
            "unit": "ns",
            "range": "± 647.1955542913374"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4176703.2321920954,
            "unit": "ns",
            "range": "± 88509.33848842517"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4218736.259563577,
            "unit": "ns",
            "range": "± 65596.16369662255"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4897020.897359914,
            "unit": "ns",
            "range": "± 38538.40369141745"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4811913.768092105,
            "unit": "ns",
            "range": "± 109955.23142178624"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4874255.984644396,
            "unit": "ns",
            "range": "± 43168.99573010398"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6864627.859920058,
            "unit": "ns",
            "range": "± 159623.75674153262"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4766.057784489223,
            "unit": "ns",
            "range": "± 27.647982949445872"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4729.316120910645,
            "unit": "ns",
            "range": "± 25.418696394345968"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4894.153620147705,
            "unit": "ns",
            "range": "± 31.82248383493363"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2216.71776935972,
            "unit": "ns",
            "range": "± 15.894208403262894"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2149.676627976554,
            "unit": "ns",
            "range": "± 11.829074740515422"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2266.226303918021,
            "unit": "ns",
            "range": "± 9.766349070932646"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1613187.5934375,
            "unit": "ns",
            "range": "± 17204.613776811846"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1615566.41375,
            "unit": "ns",
            "range": "± 12545.77701840539"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1600493.2244921876,
            "unit": "ns",
            "range": "± 3058.9752530726682"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 696601.7005440848,
            "unit": "ns",
            "range": "± 2492.1209364487772"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 680634.3924654447,
            "unit": "ns",
            "range": "± 4172.399838881044"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 699920.0143790409,
            "unit": "ns",
            "range": "± 3004.109649844457"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81429.03888888888,
            "unit": "ns",
            "range": "± 6328.00842258009"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28346.0395480226,
            "unit": "ns",
            "range": "± 5899.952622220774"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 87145.27894736842,
            "unit": "ns",
            "range": "± 10573.349284330083"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26130.625,
            "unit": "ns",
            "range": "± 1368.1699128859382"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 89984.93229166667,
            "unit": "ns",
            "range": "± 9805.19291525359"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 75834.71374045801,
            "unit": "ns",
            "range": "± 5352.170648448854"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 149031.64462809917,
            "unit": "ns",
            "range": "± 14020.900375783947"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 139348.4814814815,
            "unit": "ns",
            "range": "± 11494.83642151414"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 90788.52717391304,
            "unit": "ns",
            "range": "± 6947.302968402676"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83787.12760416667,
            "unit": "ns",
            "range": "± 6624.426995915028"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 94552.86092715232,
            "unit": "ns",
            "range": "± 8826.026026126545"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 79487.16666666667,
            "unit": "ns",
            "range": "± 4596.1493108013265"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2061176.5862068965,
            "unit": "ns",
            "range": "± 20129.144163407236"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1726665.7142857143,
            "unit": "ns",
            "range": "± 18380.774504814137"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2072099.8333333333,
            "unit": "ns",
            "range": "± 26581.663243326384"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1726760.7,
            "unit": "ns",
            "range": "± 14663.74441090255"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2007429.696969697,
            "unit": "ns",
            "range": "± 38120.88671457293"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1954599.2068965517,
            "unit": "ns",
            "range": "± 14542.450663928961"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1623614.6513761468,
            "unit": "ns",
            "range": "± 93632.21200708814"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1400415.9655172413,
            "unit": "ns",
            "range": "± 14453.01625337068"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 2461523.5222222223,
            "unit": "ns",
            "range": "± 1858482.5726196605"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1327202.9833333334,
            "unit": "ns",
            "range": "± 10260.588280072487"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 2765849.9859154928,
            "unit": "ns",
            "range": "± 2216898.759917605"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1416813.462962963,
            "unit": "ns",
            "range": "± 11147.51633421779"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "f8aa1ba8fa5af4d2e3e797e20f0fabee38619fe1",
          "message": "Merge pull request #137 from marius-bughiu/fix/issue-136-long-dictionary-enumeration-tests\n\ntest(collections): add LongDictionaryEnumerationTests to close parity gap",
          "timestamp": "2026-06-03T23:27:41+03:00",
          "tree_id": "bb0d133d3a674094ada88d38c89f739374fa3cf8",
          "url": "https://github.com/marius-bughiu/Celerity/commit/f8aa1ba8fa5af4d2e3e797e20f0fabee38619fe1"
        },
        "date": 1780520438997,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12589.40887594223,
            "unit": "ns",
            "range": "± 308.3527264098438"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12225.192272380247,
            "unit": "ns",
            "range": "± 410.50924937297384"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14055.218339618883,
            "unit": "ns",
            "range": "± 755.2820139383416"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9089.340693812217,
            "unit": "ns",
            "range": "± 183.732974522906"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8829.423858642578,
            "unit": "ns",
            "range": "± 135.45908999401715"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9440.218552652996,
            "unit": "ns",
            "range": "± 148.74009518232245"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5032188.017671131,
            "unit": "ns",
            "range": "± 137075.36485449265"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5099936.264787947,
            "unit": "ns",
            "range": "± 80254.82667950823"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4440562.686365928,
            "unit": "ns",
            "range": "± 75162.15461325862"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3176326.819140625,
            "unit": "ns",
            "range": "± 26613.508295432384"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3187905.3038793104,
            "unit": "ns",
            "range": "± 36460.580579415866"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6073490.62890625,
            "unit": "ns",
            "range": "± 32588.6287929332"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4794.109979629517,
            "unit": "ns",
            "range": "± 48.26570199101856"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4981.003957895132,
            "unit": "ns",
            "range": "± 249.16471313258916"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4746.500433513096,
            "unit": "ns",
            "range": "± 7.374520696732885"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1860.2833883351293,
            "unit": "ns",
            "range": "± 8.201976038827393"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1803.4094051492625,
            "unit": "ns",
            "range": "± 5.883604111927647"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2009.0117937285324,
            "unit": "ns",
            "range": "± 9.562266836394935"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1583440.8349609375,
            "unit": "ns",
            "range": "± 18113.634280927286"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1628459.2288411458,
            "unit": "ns",
            "range": "± 66485.1683364381"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1646555.8783830914,
            "unit": "ns",
            "range": "± 12736.241415370894"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 595752.8349609375,
            "unit": "ns",
            "range": "± 2175.756982417771"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 574090.9746636285,
            "unit": "ns",
            "range": "± 1237.238633782285"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 642895.0674176897,
            "unit": "ns",
            "range": "± 2612.728375451959"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13227.57440136325,
            "unit": "ns",
            "range": "± 276.85754185426293"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13118.352025349936,
            "unit": "ns",
            "range": "± 105.57747758011679"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14645.954803466797,
            "unit": "ns",
            "range": "± 256.0657013109858"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 11735.176441587251,
            "unit": "ns",
            "range": "± 126.62085740499808"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11134.082514006515,
            "unit": "ns",
            "range": "± 125.856793616834"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11699.881182966561,
            "unit": "ns",
            "range": "± 340.8394907259043"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4169255.0035511362,
            "unit": "ns",
            "range": "± 102421.67950874213"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4119619.017578125,
            "unit": "ns",
            "range": "± 71249.26681832677"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5015893.021821121,
            "unit": "ns",
            "range": "± 47817.67512495304"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4735886.309709822,
            "unit": "ns",
            "range": "± 29202.65647468882"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4884449.83828125,
            "unit": "ns",
            "range": "± 44222.3472203032"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6821493.443229167,
            "unit": "ns",
            "range": "± 96391.88078007124"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4742.329359909584,
            "unit": "ns",
            "range": "± 10.520798151308382"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4732.471692911784,
            "unit": "ns",
            "range": "± 15.916935200785122"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5039.917934926351,
            "unit": "ns",
            "range": "± 83.58400691415208"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 5953.40630891588,
            "unit": "ns",
            "range": "± 3957.5715657424175"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2149.9359994111237,
            "unit": "ns",
            "range": "± 8.180271359328007"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2280.5859519695414,
            "unit": "ns",
            "range": "± 8.83320634212465"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1597342.9179350755,
            "unit": "ns",
            "range": "± 5611.655277601678"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1600398.972439236,
            "unit": "ns",
            "range": "± 4119.656276710642"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1601727.8530441811,
            "unit": "ns",
            "range": "± 6067.473316044149"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 695842.4950498383,
            "unit": "ns",
            "range": "± 2195.7423627332023"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 676509.7893254206,
            "unit": "ns",
            "range": "± 5338.7803566104185"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 708630.664171007,
            "unit": "ns",
            "range": "± 3764.584044794526"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 85577.47567567567,
            "unit": "ns",
            "range": "± 7420.909875194808"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27247.127118644068,
            "unit": "ns",
            "range": "± 3581.414524929226"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79967.3440860215,
            "unit": "ns",
            "range": "± 6835.418168262925"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 31405.982608695653,
            "unit": "ns",
            "range": "± 4141.555695465443"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83548.42328042327,
            "unit": "ns",
            "range": "± 7063.452418980385"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 73288.87912087912,
            "unit": "ns",
            "range": "± 4324.96281518244"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 145982.51592356688,
            "unit": "ns",
            "range": "± 8962.344986754515"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 133955.52112676058,
            "unit": "ns",
            "range": "± 5019.497515714254"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 98142.54054054055,
            "unit": "ns",
            "range": "± 5228.8069641932725"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 85205.8953488372,
            "unit": "ns",
            "range": "± 6376.342903800986"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 88215.76978417266,
            "unit": "ns",
            "range": "± 5105.9811266256365"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 83382.02956989247,
            "unit": "ns",
            "range": "± 7039.290772432308"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2039658,
            "unit": "ns",
            "range": "± 11295.232334108116"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1755899.7166666666,
            "unit": "ns",
            "range": "± 18652.044102957676"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2072597.578125,
            "unit": "ns",
            "range": "± 36037.43250720619"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1741383.2931034483,
            "unit": "ns",
            "range": "± 14557.129500521412"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2021165.6166666667,
            "unit": "ns",
            "range": "± 36529.66868146082"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1979959.1333333333,
            "unit": "ns",
            "range": "± 16489.025940435895"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1564109.7,
            "unit": "ns",
            "range": "± 18226.407901534065"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1410283.5858585858,
            "unit": "ns",
            "range": "± 108710.20408833108"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1561108.5290697673,
            "unit": "ns",
            "range": "± 37165.66867754628"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1314527.7545454546,
            "unit": "ns",
            "range": "± 42330.694397721214"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1679535.2805755397,
            "unit": "ns",
            "range": "± 108088.1343894764"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1433357.107142857,
            "unit": "ns",
            "range": "± 15926.311482340778"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "1ec4b27e151daebb58af3f637d8198bfadd6fa77",
          "message": "Merge pull request #139 from marius-bughiu/test/issue-138-long-dictionary-shared-test-parity\n\ntest(collections): close LongDictionary cross-collection shared-test parity gaps",
          "timestamp": "2026-06-03T23:39:42+03:00",
          "tree_id": "2aa18aaae5b196624811d5b03e892963c6604ba9",
          "url": "https://github.com/marius-bughiu/Celerity/commit/1ec4b27e151daebb58af3f637d8198bfadd6fa77"
        },
        "date": 1780521182065,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12808.195063781739,
            "unit": "ns",
            "range": "± 201.01980456886557"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12755.520942687988,
            "unit": "ns",
            "range": "± 94.97697801349301"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13411.077103791413,
            "unit": "ns",
            "range": "± 63.834374913429635"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9178.943475341797,
            "unit": "ns",
            "range": "± 146.31882292452605"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9312.920967365133,
            "unit": "ns",
            "range": "± 49.665504269096196"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9563.829678184107,
            "unit": "ns",
            "range": "± 198.08416883134632"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5260244.626602564,
            "unit": "ns",
            "range": "± 118588.71226200912"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5232783.780395508,
            "unit": "ns",
            "range": "± 101324.23874498018"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5063680.890720274,
            "unit": "ns",
            "range": "± 117260.86081460018"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3447130.5146821123,
            "unit": "ns",
            "range": "± 15618.874044362967"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3542865.504045759,
            "unit": "ns",
            "range": "± 22302.516407933326"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6745625.20078125,
            "unit": "ns",
            "range": "± 75981.8164003924"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4720.691815694173,
            "unit": "ns",
            "range": "± 2.9927610274401304"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4710.94728415353,
            "unit": "ns",
            "range": "± 8.602853223605646"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5063.266509162055,
            "unit": "ns",
            "range": "± 7.2857351450530565"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1920.500080426534,
            "unit": "ns",
            "range": "± 20.549138994274788"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1922.8130471547445,
            "unit": "ns",
            "range": "± 17.063031010032752"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2092.9105638964425,
            "unit": "ns",
            "range": "± 3.9159367563469574"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1508939.5458233173,
            "unit": "ns",
            "range": "± 1769.9199815066936"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1533050.071439303,
            "unit": "ns",
            "range": "± 31775.871042837887"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1582387.02296875,
            "unit": "ns",
            "range": "± 15999.817484325855"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 551425.7004394531,
            "unit": "ns",
            "range": "± 2892.438781270292"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 492046.69436743954,
            "unit": "ns",
            "range": "± 9980.47735666339"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 588222.6451396004,
            "unit": "ns",
            "range": "± 21028.445304159497"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13422.368592943463,
            "unit": "ns",
            "range": "± 108.71059148095468"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13854.94877208363,
            "unit": "ns",
            "range": "± 448.850190816679"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15538.342300959996,
            "unit": "ns",
            "range": "± 262.2458756730527"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9052.853259159969,
            "unit": "ns",
            "range": "± 292.07079794260466"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8591.746908890573,
            "unit": "ns",
            "range": "± 310.33152184837076"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 8899.764701054013,
            "unit": "ns",
            "range": "± 119.115379212361"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4385594.83359375,
            "unit": "ns",
            "range": "± 88066.78511933988"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4402746.352481618,
            "unit": "ns",
            "range": "± 84743.33121745657"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5205126.729631697,
            "unit": "ns",
            "range": "± 32876.45791596052"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5148108.933189655,
            "unit": "ns",
            "range": "± 51463.573280849094"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5208561.542057292,
            "unit": "ns",
            "range": "± 61763.986567594024"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7589525.007291666,
            "unit": "ns",
            "range": "± 95380.49377277856"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4722.942534520076,
            "unit": "ns",
            "range": "± 8.113961115422068"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4604.368941978172,
            "unit": "ns",
            "range": "± 3.3814615334098117"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5308.764846801758,
            "unit": "ns",
            "range": "± 265.63667176891266"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2213.693634490967,
            "unit": "ns",
            "range": "± 6.983832331272151"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2087.051978217231,
            "unit": "ns",
            "range": "± 4.2054940107617025"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2284.3687258753284,
            "unit": "ns",
            "range": "± 15.435808148967363"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1599763.3197737068,
            "unit": "ns",
            "range": "± 8487.61492370903"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1494071.533460115,
            "unit": "ns",
            "range": "± 47552.87108789496"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1588156.472873264,
            "unit": "ns",
            "range": "± 2198.785132041203"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 1045387.5813240841,
            "unit": "ns",
            "range": "± 335790.8041678671"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 765948.868223852,
            "unit": "ns",
            "range": "± 22194.22762399891"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 695962.3357872596,
            "unit": "ns",
            "range": "± 5032.947479063983"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81645.75899280576,
            "unit": "ns",
            "range": "± 6487.627779689506"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 30460.885057471263,
            "unit": "ns",
            "range": "± 2704.8912276070987"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80003.94240837697,
            "unit": "ns",
            "range": "± 6945.816730562396"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29782.546875,
            "unit": "ns",
            "range": "± 4386.32486900252"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 82431.02976190476,
            "unit": "ns",
            "range": "± 7105.129980586216"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 71289.53289473684,
            "unit": "ns",
            "range": "± 4986.431784283038"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 147208.87037037036,
            "unit": "ns",
            "range": "± 9308.042909719463"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 145068.2068965517,
            "unit": "ns",
            "range": "± 2624.3248332980857"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 102280.70175438597,
            "unit": "ns",
            "range": "± 12860.873342880102"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 82442.83846153846,
            "unit": "ns",
            "range": "± 8420.905810596376"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 84288.10144927536,
            "unit": "ns",
            "range": "± 6142.635694712335"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 76744.97297297297,
            "unit": "ns",
            "range": "± 5441.4959524337155"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2033143.8113207547,
            "unit": "ns",
            "range": "± 65119.8479282199"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1731278.9054054054,
            "unit": "ns",
            "range": "± 53902.70138585071"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2081254.6711711711,
            "unit": "ns",
            "range": "± 140276.71143013105"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1656338.5833333333,
            "unit": "ns",
            "range": "± 14857.14516311894"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1978970.948275862,
            "unit": "ns",
            "range": "± 33150.82659289014"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1942564.82,
            "unit": "ns",
            "range": "± 29824.768313182252"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1762004.8086956523,
            "unit": "ns",
            "range": "± 88238.65533133641"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1413218.5168539325,
            "unit": "ns",
            "range": "± 53347.24603916102"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1920436.0588235294,
            "unit": "ns",
            "range": "± 246672.3571052329"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1465020.6627906978,
            "unit": "ns",
            "range": "± 37180.568175030705"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1842603.4512195121,
            "unit": "ns",
            "range": "± 129060.09391501865"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1539388.0512820513,
            "unit": "ns",
            "range": "± 53505.217617560746"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "f1f927d2416c9152ac547a840bd5de8c65bde7ce",
          "message": "Merge pull request #141 from marius-bughiu/feat/issue-24-string-siphash13-hasher\n\nfeat(hashing): add StringSipHash13Hasher (SipHash-1-3) for string keys",
          "timestamp": "2026-06-04T00:08:58+03:00",
          "tree_id": "1f9f8aca66a1b4947e80aa82e2bb156d0b8a8c3f",
          "url": "https://github.com/marius-bughiu/Celerity/commit/f1f927d2416c9152ac547a840bd5de8c65bde7ce"
        },
        "date": 1780522862095,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12874.59250858852,
            "unit": "ns",
            "range": "± 154.34529510379258"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13325.224000872988,
            "unit": "ns",
            "range": "± 328.8949928276738"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14132.803193717167,
            "unit": "ns",
            "range": "± 176.29937077418933"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9271.799567437942,
            "unit": "ns",
            "range": "± 166.45972797441692"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9489.635152093295,
            "unit": "ns",
            "range": "± 109.67965239894862"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10390.62706565857,
            "unit": "ns",
            "range": "± 212.93602558664068"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5271858.632440476,
            "unit": "ns",
            "range": "± 126683.40658836381"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5334020.350651042,
            "unit": "ns",
            "range": "± 104584.59788120599"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5051908.5141521515,
            "unit": "ns",
            "range": "± 180084.4248442299"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3478317.353655134,
            "unit": "ns",
            "range": "± 19597.321810646983"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3579396.765234375,
            "unit": "ns",
            "range": "± 38163.121641757505"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6785129.450651041,
            "unit": "ns",
            "range": "± 65988.12051241114"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4726.124953093352,
            "unit": "ns",
            "range": "± 3.6975375886673985"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4727.380960736956,
            "unit": "ns",
            "range": "± 24.355915235632725"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5089.7154931640625,
            "unit": "ns",
            "range": "± 5.982119184528505"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1912.20881547599,
            "unit": "ns",
            "range": "± 23.365744826840242"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1922.7379044850668,
            "unit": "ns",
            "range": "± 18.175856724200113"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2091.4479134171097,
            "unit": "ns",
            "range": "± 7.530393186282445"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1508754.0436314174,
            "unit": "ns",
            "range": "± 5446.293211598038"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1520078.686593192,
            "unit": "ns",
            "range": "± 5059.47156742062"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1573907.264299665,
            "unit": "ns",
            "range": "± 8071.1595819632075"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 550257.0908876617,
            "unit": "ns",
            "range": "± 2220.1219807700413"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 491068.46357783565,
            "unit": "ns",
            "range": "± 6339.627551901746"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 585830.0065833782,
            "unit": "ns",
            "range": "± 9547.867823002429"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14135.85274810791,
            "unit": "ns",
            "range": "± 307.02174681026986"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14181.345512127054,
            "unit": "ns",
            "range": "± 159.93364282972"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15813.05180269129,
            "unit": "ns",
            "range": "± 419.05639738178434"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9010.315287813228,
            "unit": "ns",
            "range": "± 275.96261263649285"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9152.62647363414,
            "unit": "ns",
            "range": "± 238.7942556589783"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9459.914925936995,
            "unit": "ns",
            "range": "± 192.222850189943"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4382572.498119213,
            "unit": "ns",
            "range": "± 56245.92694204796"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4396657.242779356,
            "unit": "ns",
            "range": "± 85643.4300858198"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5283624.46390625,
            "unit": "ns",
            "range": "± 40035.68988235762"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5199346.54436384,
            "unit": "ns",
            "range": "± 76579.7311562812"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5234886.262276785,
            "unit": "ns",
            "range": "± 64927.86415157801"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7660891.780859375,
            "unit": "ns",
            "range": "± 119079.9391462297"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4717.783144350405,
            "unit": "ns",
            "range": "± 8.642590668819944"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4602.04502596174,
            "unit": "ns",
            "range": "± 7.484507581336866"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5025.1919476645335,
            "unit": "ns",
            "range": "± 42.632677811503214"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2240.51352139177,
            "unit": "ns",
            "range": "± 12.396456849172992"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2078.067244918258,
            "unit": "ns",
            "range": "± 9.336632668262677"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2289.6966626093936,
            "unit": "ns",
            "range": "± 6.089482980267848"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1606982.5052083333,
            "unit": "ns",
            "range": "± 9106.09662618647"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1538249.2439903845,
            "unit": "ns",
            "range": "± 2076.7341823314805"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1579010.0764322917,
            "unit": "ns",
            "range": "± 19500.537677187152"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 704368.5083007812,
            "unit": "ns",
            "range": "± 2159.9346981193717"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 765075.6272110849,
            "unit": "ns",
            "range": "± 20256.307838053883"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 705022.0452799479,
            "unit": "ns",
            "range": "± 6377.748945124208"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79474.72527472528,
            "unit": "ns",
            "range": "± 6003.322422558355"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 28303.81679389313,
            "unit": "ns",
            "range": "± 3094.1556218670025"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83007.52879581152,
            "unit": "ns",
            "range": "± 7227.440327846883"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26729.5,
            "unit": "ns",
            "range": "± 1965.480050386458"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83473.64480874316,
            "unit": "ns",
            "range": "± 6373.9461494433035"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 74587.55555555556,
            "unit": "ns",
            "range": "± 5020.256247017106"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 151122.97435897434,
            "unit": "ns",
            "range": "± 8723.17446719073"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 141139.0942408377,
            "unit": "ns",
            "range": "± 9322.176038125906"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 88612.57051282052,
            "unit": "ns",
            "range": "± 6614.499557899106"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 81801.58148148148,
            "unit": "ns",
            "range": "± 6795.429166212588"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 83288.96276595745,
            "unit": "ns",
            "range": "± 5471.3254182904175"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 84360.31578947368,
            "unit": "ns",
            "range": "± 3692.622380930969"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2045115.4318181819,
            "unit": "ns",
            "range": "± 48770.41746708601"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1679288.1296296297,
            "unit": "ns",
            "range": "± 16818.183601269535"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2023246.7857142857,
            "unit": "ns",
            "range": "± 30825.891323339"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1704090.4838709678,
            "unit": "ns",
            "range": "± 37419.04630075524"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2062159.322147651,
            "unit": "ns",
            "range": "± 134401.76588735273"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 2155546.4153846153,
            "unit": "ns",
            "range": "± 161108.18677498828"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1769329.3703703703,
            "unit": "ns",
            "range": "± 71320.55774958586"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1419123.731060606,
            "unit": "ns",
            "range": "± 63417.35188146881"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1734219.785046729,
            "unit": "ns",
            "range": "± 61892.9169076733"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1453718.28,
            "unit": "ns",
            "range": "± 55929.4728050571"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1987602.3058823529,
            "unit": "ns",
            "range": "± 227211.17717064402"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1960617.4974747475,
            "unit": "ns",
            "range": "± 265003.5175318289"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "76ee410bf6a010f2c8cdd1f0129d108d8a93ca83",
          "message": "Merge pull request #142 from marius-bughiu/feat/issue-24-string-jenkins-oaat-hasher\n\nfeat(hashing): add StringJenkinsOaatHasher for string keys",
          "timestamp": "2026-06-04T00:21:29+03:00",
          "tree_id": "f3478a8af19155e1e461926792349f6000dcafab",
          "url": "https://github.com/marius-bughiu/Celerity/commit/76ee410bf6a010f2c8cdd1f0129d108d8a93ca83"
        },
        "date": 1780523804400,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14812.588345231681,
            "unit": "ns",
            "range": "± 198.69908421742457"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14943.833501881567,
            "unit": "ns",
            "range": "± 117.50650421244337"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 16681.270947105008,
            "unit": "ns",
            "range": "± 385.81850271422394"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8879.828917326751,
            "unit": "ns",
            "range": "± 34.42247968070696"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8951.659610203335,
            "unit": "ns",
            "range": "± 59.9620372114542"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10289.983052220838,
            "unit": "ns",
            "range": "± 124.21932412712313"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4435345.982776988,
            "unit": "ns",
            "range": "± 333121.83571230446"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4483458.392617187,
            "unit": "ns",
            "range": "± 316481.9990069524"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4405377.58153409,
            "unit": "ns",
            "range": "± 248345.95129176273"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3350687.269856771,
            "unit": "ns",
            "range": "± 54943.23764438959"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3380619.723111979,
            "unit": "ns",
            "range": "± 43926.222677275866"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6173897.8809267245,
            "unit": "ns",
            "range": "± 73808.65184322422"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4221.489969889323,
            "unit": "ns",
            "range": "± 6.140094045276942"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4030.5859442490796,
            "unit": "ns",
            "range": "± 19.34389672189799"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4299.239567389855,
            "unit": "ns",
            "range": "± 107.92341067359303"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1739.28879404068,
            "unit": "ns",
            "range": "± 29.96834462054436"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1759.1294367754901,
            "unit": "ns",
            "range": "± 18.229306259333786"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1924.9234285354614,
            "unit": "ns",
            "range": "± 5.526607423911835"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1436515.8664899555,
            "unit": "ns",
            "range": "± 13577.342640237386"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1440257.8535907452,
            "unit": "ns",
            "range": "± 12175.680067770281"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1536299.2843480604,
            "unit": "ns",
            "range": "± 9939.162701245748"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 540309.5694032867,
            "unit": "ns",
            "range": "± 6051.368872147355"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 538117.6356580011,
            "unit": "ns",
            "range": "± 2796.9196303856793"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 662990.1359230324,
            "unit": "ns",
            "range": "± 12117.731194462554"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15914.74149726419,
            "unit": "ns",
            "range": "± 287.6414773118507"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16040.353884992928,
            "unit": "ns",
            "range": "± 240.06151418642446"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 19969.473653548805,
            "unit": "ns",
            "range": "± 807.7824142758261"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9895.5012455693,
            "unit": "ns",
            "range": "± 126.00099736884825"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9617.67014169693,
            "unit": "ns",
            "range": "± 164.58272360088245"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11599.859537601471,
            "unit": "ns",
            "range": "± 273.9562062179099"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4162578.7411858975,
            "unit": "ns",
            "range": "± 94552.27306477615"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4344694.661626345,
            "unit": "ns",
            "range": "± 216071.9428626116"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5254047.488198138,
            "unit": "ns",
            "range": "± 137084.96343666094"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4847501.028505067,
            "unit": "ns",
            "range": "± 105257.68260584975"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4858499.845200893,
            "unit": "ns",
            "range": "± 98881.67801122443"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6659112.540494791,
            "unit": "ns",
            "range": "± 75233.91465794946"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4130.109799702962,
            "unit": "ns",
            "range": "± 5.007915667973988"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4157.360929761614,
            "unit": "ns",
            "range": "± 6.2798792050421115"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4280.505504314716,
            "unit": "ns",
            "range": "± 5.702004426203687"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2139.49553665748,
            "unit": "ns",
            "range": "± 5.206466153069895"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2081.9967344724214,
            "unit": "ns",
            "range": "± 15.614422547594057"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2205.5547823224747,
            "unit": "ns",
            "range": "± 5.211858771873045"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1446309.8539225261,
            "unit": "ns",
            "range": "± 56447.997437615086"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1516159.0242396763,
            "unit": "ns",
            "range": "± 22493.841296607567"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1649283.1931152344,
            "unit": "ns",
            "range": "± 53797.36894117984"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 693956.4743326823,
            "unit": "ns",
            "range": "± 12488.35882921782"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 677985.3105637123,
            "unit": "ns",
            "range": "± 1240.2022060530421"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 706083.0418836805,
            "unit": "ns",
            "range": "± 16811.877665026575"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74466.93537414966,
            "unit": "ns",
            "range": "± 8080.538974859261"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27571.402116402118,
            "unit": "ns",
            "range": "± 4151.474187946614"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74415.05837563451,
            "unit": "ns",
            "range": "± 8090.281532707169"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26523.07037037037,
            "unit": "ns",
            "range": "± 2035.4920470840457"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 77938.57329842931,
            "unit": "ns",
            "range": "± 8005.027619852355"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 65648.42857142857,
            "unit": "ns",
            "range": "± 6857.9264952724925"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 126311.89285714286,
            "unit": "ns",
            "range": "± 5669.878039460637"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 125897.91282051282,
            "unit": "ns",
            "range": "± 8181.439768031513"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 83819.05555555556,
            "unit": "ns",
            "range": "± 6168.996282082889"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 73437.09585492229,
            "unit": "ns",
            "range": "± 7128.185117011102"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 81325.9607329843,
            "unit": "ns",
            "range": "± 6922.9558080937095"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 75491.148989899,
            "unit": "ns",
            "range": "± 7949.282638948134"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1932477.0394736843,
            "unit": "ns",
            "range": "± 43996.45028085751"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1602014.4324324324,
            "unit": "ns",
            "range": "± 40634.93148008424"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1939984.5777777778,
            "unit": "ns",
            "range": "± 97369.96071087108"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1557228.9333333333,
            "unit": "ns",
            "range": "± 29230.71467407992"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1951250.1793478262,
            "unit": "ns",
            "range": "± 72940.61259338105"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1931433.15,
            "unit": "ns",
            "range": "± 24742.256098225407"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1712766.116766467,
            "unit": "ns",
            "range": "± 91639.02225427613"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1405131.8113207547,
            "unit": "ns",
            "range": "± 78841.01745331284"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1715869.9539473683,
            "unit": "ns",
            "range": "± 90636.38708246319"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1354507.1650485436,
            "unit": "ns",
            "range": "± 58552.07145698489"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1953257.3780487804,
            "unit": "ns",
            "range": "± 215430.49465698615"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1596330.5856353592,
            "unit": "ns",
            "range": "± 99488.93976995417"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e7f21f0e34ab3243cb5638d987a9330aa10d6521",
          "message": "Merge pull request #143 from marius-bughiu/feat/issue-24-string-xxhash3-hasher\n\nfeat(hashing): add StringXxHash3Hasher for string keys",
          "timestamp": "2026-06-04T00:44:25+03:00",
          "tree_id": "b6345c033116ec4ee142b01cca7bcc7708c1145e",
          "url": "https://github.com/marius-bughiu/Celerity/commit/e7f21f0e34ab3243cb5638d987a9330aa10d6521"
        },
        "date": 1780525131790,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12258.22870014832,
            "unit": "ns",
            "range": "± 865.4391626873988"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12503.602932248798,
            "unit": "ns",
            "range": "± 353.01191343584367"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 17344.517213390718,
            "unit": "ns",
            "range": "± 3965.590115472954"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8756.79084721318,
            "unit": "ns",
            "range": "± 25.58015699013869"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8548.18719587655,
            "unit": "ns",
            "range": "± 68.56197300716535"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9234.268251342774,
            "unit": "ns",
            "range": "± 95.44128393434134"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5035639.659598215,
            "unit": "ns",
            "range": "± 98429.33991948281"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5034500.976356908,
            "unit": "ns",
            "range": "± 114437.21333318843"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4385574.550048828,
            "unit": "ns",
            "range": "± 95378.34078675673"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3167003.708622685,
            "unit": "ns",
            "range": "± 25152.70143532737"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3183186.6830729167,
            "unit": "ns",
            "range": "± 28794.875580683445"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6063599.679148707,
            "unit": "ns",
            "range": "± 33782.64472151802"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4762.523520605905,
            "unit": "ns",
            "range": "± 24.769606641984883"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4735.578659875052,
            "unit": "ns",
            "range": "± 6.276261738250716"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4797.4142321073095,
            "unit": "ns",
            "range": "± 56.026544422647476"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1864.72777346907,
            "unit": "ns",
            "range": "± 7.147458307172644"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1797.480858929952,
            "unit": "ns",
            "range": "± 6.783146219993865"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2010.1902151107788,
            "unit": "ns",
            "range": "± 5.686231578121891"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1569004.84047154,
            "unit": "ns",
            "range": "± 16007.463142705996"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1571519.788997396,
            "unit": "ns",
            "range": "± 15006.074464823407"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1632393.754720052,
            "unit": "ns",
            "range": "± 18344.444528216383"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 591246.726526331,
            "unit": "ns",
            "range": "± 2819.002947829575"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 575395.5686961206,
            "unit": "ns",
            "range": "± 2075.0346611378072"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 642875.9920191271,
            "unit": "ns",
            "range": "± 3697.6887304069796"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14205.089860769418,
            "unit": "ns",
            "range": "± 344.60065935656417"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14438.383368450663,
            "unit": "ns",
            "range": "± 813.1609636802226"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15057.74950961409,
            "unit": "ns",
            "range": "± 155.37979983489302"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 12159.954156283675,
            "unit": "ns",
            "range": "± 192.38686276714944"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 11648.144090505746,
            "unit": "ns",
            "range": "± 435.753163761894"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 11778.629451892994,
            "unit": "ns",
            "range": "± 470.56093786507773"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4206952.070004112,
            "unit": "ns",
            "range": "± 104875.93867277456"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4279934.407477679,
            "unit": "ns",
            "range": "± 88116.39680487514"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4994669.79609375,
            "unit": "ns",
            "range": "± 58751.6707838669"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4781104.2949892245,
            "unit": "ns",
            "range": "± 55719.21398377519"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5035387.9668642245,
            "unit": "ns",
            "range": "± 76236.5605268607"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6795967.692940848,
            "unit": "ns",
            "range": "± 82709.41072450556"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4744.476980538204,
            "unit": "ns",
            "range": "± 16.97202871059158"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4731.8236852010095,
            "unit": "ns",
            "range": "± 19.53045002037244"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4888.446185934133,
            "unit": "ns",
            "range": "± 26.140894007675772"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2226.1221961975098,
            "unit": "ns",
            "range": "± 16.405994846238375"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2144.31047157005,
            "unit": "ns",
            "range": "± 7.326391993745384"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2263.4896855847587,
            "unit": "ns",
            "range": "± 15.791548023190996"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1614402.4111979166,
            "unit": "ns",
            "range": "± 12587.314740560209"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1557359.1430844907,
            "unit": "ns",
            "range": "± 41327.22108926584"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1609278.5239762932,
            "unit": "ns",
            "range": "± 15486.619791221252"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 715544.6408607219,
            "unit": "ns",
            "range": "± 20426.207023167426"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 679917.2828194754,
            "unit": "ns",
            "range": "± 1946.01285098239"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 715157.6387416294,
            "unit": "ns",
            "range": "± 2528.7759213045906"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 81611.54812834224,
            "unit": "ns",
            "range": "± 6971.969101594269"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26439.863636363636,
            "unit": "ns",
            "range": "± 870.9123858565705"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80528.9375,
            "unit": "ns",
            "range": "± 7540.130166516389"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26366.005617977527,
            "unit": "ns",
            "range": "± 2121.9200837392937"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 89158.67021276595,
            "unit": "ns",
            "range": "± 10529.899988492962"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 77772.61111111111,
            "unit": "ns",
            "range": "± 5778.00061491812"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 141680.65322580645,
            "unit": "ns",
            "range": "± 5370.152791571716"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 138364.04137931034,
            "unit": "ns",
            "range": "± 7346.788755106869"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 92355.60869565218,
            "unit": "ns",
            "range": "± 7296.071774971636"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 82591.77142857143,
            "unit": "ns",
            "range": "± 5464.500092831955"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 88338.616,
            "unit": "ns",
            "range": "± 5348.55440943716"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 89399.41034482759,
            "unit": "ns",
            "range": "± 4312.741964093212"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2064999.4,
            "unit": "ns",
            "range": "± 22087.04770916422"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1707829.9,
            "unit": "ns",
            "range": "± 13788.970428295715"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2073498.6785714286,
            "unit": "ns",
            "range": "± 15384.458124410688"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1724708.55,
            "unit": "ns",
            "range": "± 11299.589451918002"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2185485.066666667,
            "unit": "ns",
            "range": "± 231825.6824571406"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1968173.9666666666,
            "unit": "ns",
            "range": "± 14667.93657651422"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 2856548.9776119404,
            "unit": "ns",
            "range": "± 2260102.078954677"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1297643.6513761468,
            "unit": "ns",
            "range": "± 55018.82750433175"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 4162529.896551724,
            "unit": "ns",
            "range": "± 2536040.1203960725"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1450426.5438596492,
            "unit": "ns",
            "range": "± 182333.13073836043"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1972150.8146067415,
            "unit": "ns",
            "range": "± 387677.5608535829"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1423259.0689655172,
            "unit": "ns",
            "range": "± 10648.510569662629"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a5a90c7670de660fd8dc8cbe06a2e51adcde077e",
          "message": "Merge pull request #144 from marius-bughiu/feat/issue-24-string-halfsiphash24-hasher\n\nfeat(hashing): add StringHalfSipHash24Hasher for string keys",
          "timestamp": "2026-06-04T01:09:09+03:00",
          "tree_id": "24e92e1e81966202f5edf8c2941c44e951af02c5",
          "url": "https://github.com/marius-bughiu/Celerity/commit/a5a90c7670de660fd8dc8cbe06a2e51adcde077e"
        },
        "date": 1780526566712,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13655.30254706021,
            "unit": "ns",
            "range": "± 176.97006976163226"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13516.683312789253,
            "unit": "ns",
            "range": "± 338.53625691946314"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14591.367178780692,
            "unit": "ns",
            "range": "± 304.42451071636594"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9280.45615877424,
            "unit": "ns",
            "range": "± 132.36353962209213"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9810.357237679618,
            "unit": "ns",
            "range": "± 266.0017661209105"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10044.522925694784,
            "unit": "ns",
            "range": "± 231.81436003903661"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5300264.266047297,
            "unit": "ns",
            "range": "± 109267.91164717778"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5258920.183728448,
            "unit": "ns",
            "range": "± 69272.87084086011"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5212834.2958984375,
            "unit": "ns",
            "range": "± 121140.74925805174"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3534564.824757543,
            "unit": "ns",
            "range": "± 50945.3143461548"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3588364.5076171877,
            "unit": "ns",
            "range": "± 28828.383912450834"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6792227.683894231,
            "unit": "ns",
            "range": "± 54422.51345479717"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4710.576466878255,
            "unit": "ns",
            "range": "± 7.392345670344964"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5071.9537109961875,
            "unit": "ns",
            "range": "± 291.0744083824311"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5076.957082014817,
            "unit": "ns",
            "range": "± 19.890396094132058"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1933.2182839711506,
            "unit": "ns",
            "range": "± 18.489239745344094"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1917.050119136942,
            "unit": "ns",
            "range": "± 12.791048424163781"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2093.278782032154,
            "unit": "ns",
            "range": "± 4.862883910756606"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1487345.3192471592,
            "unit": "ns",
            "range": "± 44436.6992571802"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1511615.8815104167,
            "unit": "ns",
            "range": "± 4122.110465391691"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1575225.43632139,
            "unit": "ns",
            "range": "± 5468.825362820336"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 552876.8651216947,
            "unit": "ns",
            "range": "± 2830.6361779451427"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 492170.4043294271,
            "unit": "ns",
            "range": "± 7306.541188514836"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 579207.8209635416,
            "unit": "ns",
            "range": "± 13312.862684331827"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14192.402042311065,
            "unit": "ns",
            "range": "± 533.670701520168"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13535.263764801026,
            "unit": "ns",
            "range": "± 386.76303422538297"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15878.04084218343,
            "unit": "ns",
            "range": "± 233.50080427056338"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9632.235507583619,
            "unit": "ns",
            "range": "± 224.89210116343904"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8532.586169481277,
            "unit": "ns",
            "range": "± 265.0005138209585"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 8909.200577045309,
            "unit": "ns",
            "range": "± 123.2441500088599"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4434594.811434659,
            "unit": "ns",
            "range": "± 90598.4933006409"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4454618.753472222,
            "unit": "ns",
            "range": "± 112890.39030776276"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5312798.739897629,
            "unit": "ns",
            "range": "± 55873.04806479998"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5283678.123697917,
            "unit": "ns",
            "range": "± 76592.19272182112"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5207357.790755209,
            "unit": "ns",
            "range": "± 58834.22667385892"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7687828.193098959,
            "unit": "ns",
            "range": "± 97090.71299611988"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4725.542180574857,
            "unit": "ns",
            "range": "± 7.951152089326073"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4883.009201965332,
            "unit": "ns",
            "range": "± 273.6982682811789"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5025.327993120466,
            "unit": "ns",
            "range": "± 35.920380829334285"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2231.2288525899253,
            "unit": "ns",
            "range": "± 26.317966019747974"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2088.434173075358,
            "unit": "ns",
            "range": "± 14.561760281208661"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2283.471895472209,
            "unit": "ns",
            "range": "± 7.342632922233066"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1612322.168247768,
            "unit": "ns",
            "range": "± 4433.607302236924"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1505302.5998186383,
            "unit": "ns",
            "range": "± 35846.06371311241"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1594876.4383755387,
            "unit": "ns",
            "range": "± 4976.337774455976"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 702426.1311848959,
            "unit": "ns",
            "range": "± 4317.811629327561"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 766072.6823893229,
            "unit": "ns",
            "range": "± 9858.580631321081"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 661641.2802566002,
            "unit": "ns",
            "range": "± 48911.3925336776"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 76438.8125,
            "unit": "ns",
            "range": "± 6944.227325477311"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29591.17261904762,
            "unit": "ns",
            "range": "± 3096.2296107297298"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79363.39722222222,
            "unit": "ns",
            "range": "± 6855.799938391117"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29697.516853932586,
            "unit": "ns",
            "range": "± 3327.434526048269"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 83487.31578947368,
            "unit": "ns",
            "range": "± 7142.541622123315"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70470.26111111112,
            "unit": "ns",
            "range": "± 4515.3814594933365"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 150467.9135802469,
            "unit": "ns",
            "range": "± 11442.911037591108"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 146682.58771929826,
            "unit": "ns",
            "range": "± 13208.084177436745"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 86383.75773195876,
            "unit": "ns",
            "range": "± 6830.157337081791"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 83448.83937823834,
            "unit": "ns",
            "range": "± 7915.5654896502065"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 84949.89682539682,
            "unit": "ns",
            "range": "± 7458.05041807177"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 75168.64438502674,
            "unit": "ns",
            "range": "± 7061.840364894688"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2010073.892857143,
            "unit": "ns",
            "range": "± 17622.35461752694"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1672796.173076923,
            "unit": "ns",
            "range": "± 14399.395974097182"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1999310.6785714286,
            "unit": "ns",
            "range": "± 15342.971410638549"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1713762.4310344828,
            "unit": "ns",
            "range": "± 28291.313055841812"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2013402.5897435897,
            "unit": "ns",
            "range": "± 47115.39507671728"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1945627.1896551724,
            "unit": "ns",
            "range": "± 17372.87209550208"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1739293.7846153846,
            "unit": "ns",
            "range": "± 49625.35947051553"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1396295.206521739,
            "unit": "ns",
            "range": "± 38833.12021676943"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 6460958.6034482755,
            "unit": "ns",
            "range": "± 79495.30228498898"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1468075.1964285714,
            "unit": "ns",
            "range": "± 24668.2009467802"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1979316.494117647,
            "unit": "ns",
            "range": "± 274952.6468681718"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1595894.0796703296,
            "unit": "ns",
            "range": "± 91710.38656874651"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8d964eb1e897a767ce606b558ea71db320d559d5",
          "message": "Merge pull request #147 from marius-bughiu/feat/issue-24-string-djb2-hasher\n\nfeat(hashing): add StringDjb2Hasher for string keys",
          "timestamp": "2026-06-04T08:38:34+03:00",
          "tree_id": "7918d68653853da572f8e5c282e91503ab3a2255",
          "url": "https://github.com/marius-bughiu/Celerity/commit/8d964eb1e897a767ce606b558ea71db320d559d5"
        },
        "date": 1780553620803,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12821.250372524919,
            "unit": "ns",
            "range": "± 202.6899557856104"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13625.620155490175,
            "unit": "ns",
            "range": "± 378.85259484227925"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14509.901372797349,
            "unit": "ns",
            "range": "± 757.7996564380011"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 8379.292215474446,
            "unit": "ns",
            "range": "± 96.55238925152369"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 8783.611784253802,
            "unit": "ns",
            "range": "± 54.272731231763"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9579.584023952484,
            "unit": "ns",
            "range": "± 164.76436728917966"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4482747.610514323,
            "unit": "ns",
            "range": "± 173503.89079460828"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4311536.014570313,
            "unit": "ns",
            "range": "± 322173.13024158764"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4047268.9387122844,
            "unit": "ns",
            "range": "± 142735.79188091078"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3138059.4663085938,
            "unit": "ns",
            "range": "± 20007.64095801051"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3242975.667829241,
            "unit": "ns",
            "range": "± 48101.869521798704"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 5785055.037527902,
            "unit": "ns",
            "range": "± 69757.79759557564"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4744.440639223371,
            "unit": "ns",
            "range": "± 535.2144532923821"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4026.1628245600946,
            "unit": "ns",
            "range": "± 14.238265411859635"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4188.260698671694,
            "unit": "ns",
            "range": "± 4.808157230710978"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1733.4190745353699,
            "unit": "ns",
            "range": "± 25.041465352383696"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1755.5835024515789,
            "unit": "ns",
            "range": "± 8.466977411162851"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 1921.2851872077356,
            "unit": "ns",
            "range": "± 1.260583587305102"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1433309.0870949074,
            "unit": "ns",
            "range": "± 7923.808455881633"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1446243.8328125,
            "unit": "ns",
            "range": "± 4090.15282525694"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1544269.8460286458,
            "unit": "ns",
            "range": "± 4884.071194594462"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 536123.9135742188,
            "unit": "ns",
            "range": "± 3562.872776170083"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 536173.330549569,
            "unit": "ns",
            "range": "± 2775.7788763684416"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 661634.9640692349,
            "unit": "ns",
            "range": "± 3440.211620986794"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14389.871743479083,
            "unit": "ns",
            "range": "± 292.11164265155185"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14071.80288198043,
            "unit": "ns",
            "range": "± 368.69896888130614"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 16958.084316029268,
            "unit": "ns",
            "range": "± 582.909998418174"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9557.709340413412,
            "unit": "ns",
            "range": "± 152.49580753502525"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9219.046685312309,
            "unit": "ns",
            "range": "± 265.8860617427897"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9776.189161078875,
            "unit": "ns",
            "range": "± 237.8707641120536"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3985629.0521282325,
            "unit": "ns",
            "range": "± 122172.9868831419"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 3917212.5100446427,
            "unit": "ns",
            "range": "± 91777.5697760562"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4765753.841674805,
            "unit": "ns",
            "range": "± 149074.7325362096"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 4549159.406860352,
            "unit": "ns",
            "range": "± 154873.98574613588"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 4652459.248278602,
            "unit": "ns",
            "range": "± 135817.01342473988"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 6241177.770450368,
            "unit": "ns",
            "range": "± 122576.93598998984"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4129.809194124662,
            "unit": "ns",
            "range": "± 4.772918179459765"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4166.858021599905,
            "unit": "ns",
            "range": "± 7.883639487521469"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4278.427134377615,
            "unit": "ns",
            "range": "± 7.4336886916529155"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2135.199631867585,
            "unit": "ns",
            "range": "± 3.22611901851451"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2073.904136657715,
            "unit": "ns",
            "range": "± 1.6752859602297636"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2203.070668084281,
            "unit": "ns",
            "range": "± 6.521966730827049"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1509567.1789641203,
            "unit": "ns",
            "range": "± 5569.053904451411"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1613255.1912715517,
            "unit": "ns",
            "range": "± 102267.68234797056"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1580342.6358095366,
            "unit": "ns",
            "range": "± 7182.826135554205"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 696633.603044181,
            "unit": "ns",
            "range": "± 3535.9429471514404"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 652329.1272321428,
            "unit": "ns",
            "range": "± 13879.110000221817"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 722727.2616514008,
            "unit": "ns",
            "range": "± 5022.432060637677"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 73287.16666666667,
            "unit": "ns",
            "range": "± 8032.74416891024"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 25656.00857142857,
            "unit": "ns",
            "range": "± 2020.1755610193632"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 73160.5358974359,
            "unit": "ns",
            "range": "± 7558.830376695404"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27317.378698224853,
            "unit": "ns",
            "range": "± 4024.644138190026"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 74851.77918781726,
            "unit": "ns",
            "range": "± 8527.767371060363"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 64696.09595959596,
            "unit": "ns",
            "range": "± 6355.718289302831"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 123695.9551724138,
            "unit": "ns",
            "range": "± 6776.940909948594"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 119583.52010050252,
            "unit": "ns",
            "range": "± 11077.787764356171"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 85469.31896551725,
            "unit": "ns",
            "range": "± 6662.859401317437"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 72880.33838383839,
            "unit": "ns",
            "range": "± 7244.033381769041"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 79582.0445026178,
            "unit": "ns",
            "range": "± 7447.855458689794"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 69835.92346938775,
            "unit": "ns",
            "range": "± 5885.367739355491"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1887988.4310344828,
            "unit": "ns",
            "range": "± 20138.33480300522"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1524988.3166666667,
            "unit": "ns",
            "range": "± 15405.585230734883"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1880222.4642857143,
            "unit": "ns",
            "range": "± 16080.28704940524"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1534133.7586206896,
            "unit": "ns",
            "range": "± 18575.86203278555"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1935389.55,
            "unit": "ns",
            "range": "± 23671.273453699687"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1871346.2142857143,
            "unit": "ns",
            "range": "± 13344.770484564891"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 3378809.45,
            "unit": "ns",
            "range": "± 3241638.8862236333"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1325657.2048192772,
            "unit": "ns",
            "range": "± 72198.70149966705"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1642545.25,
            "unit": "ns",
            "range": "± 58970.1672677503"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1360822.6724137932,
            "unit": "ns",
            "range": "± 23326.971861328504"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1750324.4292035399,
            "unit": "ns",
            "range": "± 78373.33104599385"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1547223.2016129033,
            "unit": "ns",
            "range": "± 47851.55746565638"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e22da0841938ab3d2ed3e65ddfca7ba3d7762fee",
          "message": "Merge pull request #148 from marius-bughiu/feat/issue-24-string-sdbm-hasher\n\nfeat(hashing): add StringSdbmHasher for string keys",
          "timestamp": "2026-06-04T22:17:10+03:00",
          "tree_id": "60dd0ecb63cd3a4eb9fa9291266aea6778427802",
          "url": "https://github.com/marius-bughiu/Celerity/commit/e22da0841938ab3d2ed3e65ddfca7ba3d7762fee"
        },
        "date": 1780602532062,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13185.266270955404,
            "unit": "ns",
            "range": "± 566.9869844808479"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12689.809342520577,
            "unit": "ns",
            "range": "± 143.33831549199004"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 14523.155759684245,
            "unit": "ns",
            "range": "± 204.36369982212094"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9444.131057230632,
            "unit": "ns",
            "range": "± 136.24529582591256"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9352.051020655139,
            "unit": "ns",
            "range": "± 221.316595523787"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 10220.609664372036,
            "unit": "ns",
            "range": "± 127.56306126840545"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5260542.850350215,
            "unit": "ns",
            "range": "± 70434.30005696756"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5261797.146775266,
            "unit": "ns",
            "range": "± 142223.58218020908"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5171238.808054957,
            "unit": "ns",
            "range": "± 76368.62579120569"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3467675.623604911,
            "unit": "ns",
            "range": "± 11302.532314505153"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3552578.0242456896,
            "unit": "ns",
            "range": "± 27360.89224416532"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6847177.555266203,
            "unit": "ns",
            "range": "± 58274.61434422423"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4710.703679765974,
            "unit": "ns",
            "range": "± 4.8343862297632185"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4715.215899540828,
            "unit": "ns",
            "range": "± 9.83204313326667"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5080.718557287146,
            "unit": "ns",
            "range": "± 22.54473680764975"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1905.070479801723,
            "unit": "ns",
            "range": "± 18.7970068026875"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1921.223936843872,
            "unit": "ns",
            "range": "± 18.235177165005226"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2089.8235651162954,
            "unit": "ns",
            "range": "± 4.411468526760045"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1543348.6338975695,
            "unit": "ns",
            "range": "± 35138.960228107135"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1512232.5153556035,
            "unit": "ns",
            "range": "± 2754.4458276392165"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1714404.449146412,
            "unit": "ns",
            "range": "± 133435.6766981289"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 550720.1321976273,
            "unit": "ns",
            "range": "± 2401.441832713331"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 495466.7037760417,
            "unit": "ns",
            "range": "± 8309.88162122036"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 576915.6653758081,
            "unit": "ns",
            "range": "± 9769.92444467985"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13632.4216048142,
            "unit": "ns",
            "range": "± 172.43524365994497"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13427.945381427633,
            "unit": "ns",
            "range": "± 217.69496502163523"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15322.133969624838,
            "unit": "ns",
            "range": "± 170.22610876026127"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9218.751408894857,
            "unit": "ns",
            "range": "± 291.43886224557536"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 8870.225623390892,
            "unit": "ns",
            "range": "± 182.68566187712221"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 8708.145992137768,
            "unit": "ns",
            "range": "± 109.77062659379133"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4446820.2263513515,
            "unit": "ns",
            "range": "± 90138.36933098212"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4444441.355598958,
            "unit": "ns",
            "range": "± 76355.58394584009"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5313255.362068965,
            "unit": "ns",
            "range": "± 76409.65196976779"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5183652.953125,
            "unit": "ns",
            "range": "± 42724.70742087871"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5183512.913783482,
            "unit": "ns",
            "range": "± 48732.17225184265"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7620720.429036458,
            "unit": "ns",
            "range": "± 61459.72297393437"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4724.615326472691,
            "unit": "ns",
            "range": "± 11.214696741794395"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4603.4792404174805,
            "unit": "ns",
            "range": "± 3.367261254699505"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5052.5445556640625,
            "unit": "ns",
            "range": "± 52.56428817582081"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2217.0007277897425,
            "unit": "ns",
            "range": "± 14.532156305613652"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2088.743978571009,
            "unit": "ns",
            "range": "± 6.197753384386169"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2286.9452691396077,
            "unit": "ns",
            "range": "± 8.730249484942417"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1610903.2768735532,
            "unit": "ns",
            "range": "± 6918.723642416256"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1540526.0042679398,
            "unit": "ns",
            "range": "± 3062.08729793646"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1590474.2887931035,
            "unit": "ns",
            "range": "± 15357.984019475558"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 700290.5387766769,
            "unit": "ns",
            "range": "± 16555.995126444566"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 766629.0498408565,
            "unit": "ns",
            "range": "± 25983.854848420713"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 661516.0355398996,
            "unit": "ns",
            "range": "± 34287.11894787357"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 84855.6007751938,
            "unit": "ns",
            "range": "± 8350.112647860056"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 26866.419270833332,
            "unit": "ns",
            "range": "± 3064.5242727571563"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 77567.371657754,
            "unit": "ns",
            "range": "± 7286.519552600469"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27070.674603174604,
            "unit": "ns",
            "range": "± 1464.44094905525"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80458.5104712042,
            "unit": "ns",
            "range": "± 6935.171760472714"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 70877.9765625,
            "unit": "ns",
            "range": "± 5057.150038047158"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 145886.36111111112,
            "unit": "ns",
            "range": "± 6601.458104608227"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 143228.90476190476,
            "unit": "ns",
            "range": "± 7050.221213514873"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 91457.0076923077,
            "unit": "ns",
            "range": "± 8594.030296868707"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 82296.40862944162,
            "unit": "ns",
            "range": "± 6940.161025912036"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 83611.58465608465,
            "unit": "ns",
            "range": "± 7884.023410105599"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 77002.67015706806,
            "unit": "ns",
            "range": "± 4759.747756595984"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2022443.2166666666,
            "unit": "ns",
            "range": "± 27532.855389667326"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1679554.5714285714,
            "unit": "ns",
            "range": "± 19997.392108543987"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2001094.6833333333,
            "unit": "ns",
            "range": "± 17161.8678809594"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1716125.75,
            "unit": "ns",
            "range": "± 34687.8758456805"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2004054.5675675676,
            "unit": "ns",
            "range": "± 45481.250674829695"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 2023489.4078947369,
            "unit": "ns",
            "range": "± 50593.21420860216"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1895042.8508287293,
            "unit": "ns",
            "range": "± 225080.0929493948"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1407512.2596153845,
            "unit": "ns",
            "range": "± 56005.43346620729"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1706496.4675324676,
            "unit": "ns",
            "range": "± 35467.652158464065"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1450537.5344827587,
            "unit": "ns",
            "range": "± 18855.349669983316"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1893466.8712574851,
            "unit": "ns",
            "range": "± 210132.71907362257"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1613457.8766233767,
            "unit": "ns",
            "range": "± 62381.68111250352"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "marius.bughiu@gmail.com",
            "name": "Marius Bughiu",
            "username": "marius-bughiu"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d54074106457a569f047ce076f1b02161a666269",
          "message": "Merge pull request #149 from marius-bughiu/feat/issue-24-string-fnv1-hasher\n\nfeat(hashing): add StringFnV1Hasher for string keys",
          "timestamp": "2026-06-04T22:33:44+03:00",
          "tree_id": "817a9c594bbe646555836390a3eb7252b44bae30",
          "url": "https://github.com/marius-bughiu/Celerity/commit/d54074106457a569f047ce076f1b02161a666269"
        },
        "date": 1780603602841,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13119.08139292399,
            "unit": "ns",
            "range": "± 233.97448549197108"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 12777.442675272623,
            "unit": "ns",
            "range": "± 165.295797476272"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 1000)",
            "value": 13662.03283521864,
            "unit": "ns",
            "range": "± 212.0702652694315"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 1000)",
            "value": 9471.408732364052,
            "unit": "ns",
            "range": "± 199.83372751094544"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 1000)",
            "value": 9346.908322470528,
            "unit": "ns",
            "range": "± 93.69429993568747"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 1000)",
            "value": 9671.817911783854,
            "unit": "ns",
            "range": "± 138.24175404618504"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5291731.706714527,
            "unit": "ns",
            "range": "± 110591.20892703348"
          },
          {
            "name": "IntSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 5250962.564993351,
            "unit": "ns",
            "range": "± 137120.31717121182"
          },
          {
            "name": "LongSetBenchmark.HashSet_Add(ItemCount: 100000)",
            "value": 4999087.353537088,
            "unit": "ns",
            "range": "± 195748.05660299477"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Add(ItemCount: 100000)",
            "value": 3476792.021012931,
            "unit": "ns",
            "range": "± 12868.333836945585"
          },
          {
            "name": "IntSetBenchmark.IntSet_Add(ItemCount: 100000)",
            "value": 3563499.8293372844,
            "unit": "ns",
            "range": "± 29626.565082215868"
          },
          {
            "name": "LongSetBenchmark.LongSet_Add(ItemCount: 100000)",
            "value": 6828283.531808035,
            "unit": "ns",
            "range": "± 44840.6510026491"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4715.359462343413,
            "unit": "ns",
            "range": "± 4.06806634160927"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 4718.711537581224,
            "unit": "ns",
            "range": "± 4.082561334556299"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 1000)",
            "value": 5078.654509887695,
            "unit": "ns",
            "range": "± 23.382149647369555"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 1000)",
            "value": 1920.7139556248983,
            "unit": "ns",
            "range": "± 25.084059685363247"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 1000)",
            "value": 1908.5814014303273,
            "unit": "ns",
            "range": "± 13.309295588348093"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 1000)",
            "value": 2093.632744118019,
            "unit": "ns",
            "range": "± 4.407588690522829"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1511751.2694614956,
            "unit": "ns",
            "range": "± 3388.267905542069"
          },
          {
            "name": "IntSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1501786.753313337,
            "unit": "ns",
            "range": "± 3327.47090270254"
          },
          {
            "name": "LongSetBenchmark.HashSet_Contains(ItemCount: 100000)",
            "value": 1576067.2331355168,
            "unit": "ns",
            "range": "± 4090.681928460708"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Contains(ItemCount: 100000)",
            "value": 550124.6744005927,
            "unit": "ns",
            "range": "± 3375.964893175109"
          },
          {
            "name": "IntSetBenchmark.IntSet_Contains(ItemCount: 100000)",
            "value": 488672.71197150735,
            "unit": "ns",
            "range": "± 9679.619931420882"
          },
          {
            "name": "LongSetBenchmark.LongSet_Contains(ItemCount: 100000)",
            "value": 585311.7049278846,
            "unit": "ns",
            "range": "± 11226.161643976504"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 14965.044484292308,
            "unit": "ns",
            "range": "± 270.9850622571147"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 13782.799346415202,
            "unit": "ns",
            "range": "± 253.12358214353577"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)",
            "value": 15880.151228162977,
            "unit": "ns",
            "range": "± 501.8129162122195"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 1000)",
            "value": 9503.833649953207,
            "unit": "ns",
            "range": "± 313.62913933085406"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 1000)",
            "value": 9015.825948953629,
            "unit": "ns",
            "range": "± 383.45856946696443"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 1000)",
            "value": 9056.230010114397,
            "unit": "ns",
            "range": "± 212.103458328304"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4438898.175151209,
            "unit": "ns",
            "range": "± 78888.79489771553"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 4485854.252256945,
            "unit": "ns",
            "range": "± 123299.90728820513"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Insert(ItemCount: 100000)",
            "value": 5316774.7180765085,
            "unit": "ns",
            "range": "± 55783.91230433732"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Insert(ItemCount: 100000)",
            "value": 5285108.424703664,
            "unit": "ns",
            "range": "± 64371.155546521106"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Insert(ItemCount: 100000)",
            "value": 5402031.852864583,
            "unit": "ns",
            "range": "± 83580.44465181329"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Insert(ItemCount: 100000)",
            "value": 7629246.438442888,
            "unit": "ns",
            "range": "± 79463.56286425999"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4610.8993975321455,
            "unit": "ns",
            "range": "± 10.395569997015999"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4610.626220440043,
            "unit": "ns",
            "range": "± 3.198261186766951"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5019.885709828344,
            "unit": "ns",
            "range": "± 39.146928116972006"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2227.2997539520265,
            "unit": "ns",
            "range": "± 15.33779987315623"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2080.6630883898056,
            "unit": "ns",
            "range": "± 9.581474179794832"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 1000)",
            "value": 2279.8125708443777,
            "unit": "ns",
            "range": "± 6.065150994429026"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1605808.1411458333,
            "unit": "ns",
            "range": "± 7781.248130548979"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1546255.9694234913,
            "unit": "ns",
            "range": "± 4260.205226301911"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1615786.284146013,
            "unit": "ns",
            "range": "± 19010.677986766386"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 704556.9645287298,
            "unit": "ns",
            "range": "± 10205.553119167069"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 771554.5345458984,
            "unit": "ns",
            "range": "± 6745.084833358743"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Lookup(ItemCount: 100000)",
            "value": 695260.6380208334,
            "unit": "ns",
            "range": "± 11701.762811602499"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 79950.67692307693,
            "unit": "ns",
            "range": "± 8396.916217403481"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 27568.0890052356,
            "unit": "ns",
            "range": "± 3346.4256433952355"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 80569.66836734694,
            "unit": "ns",
            "range": "± 9003.977776073189"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 29068.7125,
            "unit": "ns",
            "range": "± 3789.4442074136196"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 1000)",
            "value": 86963.46902654867,
            "unit": "ns",
            "range": "± 9460.4329826924"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 1000)",
            "value": 71822.77067669173,
            "unit": "ns",
            "range": "± 5642.401264059828"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 1000)",
            "value": 148651.93493150684,
            "unit": "ns",
            "range": "± 7473.740455124493"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 1000)",
            "value": 139673.72566371682,
            "unit": "ns",
            "range": "± 8486.956842758973"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 1000)",
            "value": 90354.29166666667,
            "unit": "ns",
            "range": "± 6303.5598218446485"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 1000)",
            "value": 79836.00785340313,
            "unit": "ns",
            "range": "± 7596.767732757421"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 1000)",
            "value": 81804.27720207254,
            "unit": "ns",
            "range": "± 6585.1627445324075"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 1000)",
            "value": 75322.43085106384,
            "unit": "ns",
            "range": "± 4845.132373815166"
          },
          {
            "name": "CelerityDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2002102.607142857,
            "unit": "ns",
            "range": "± 18750.024537938087"
          },
          {
            "name": "CeleritySetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1667101.9137931035,
            "unit": "ns",
            "range": "± 12589.35590805991"
          },
          {
            "name": "IntDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 2082564.484375,
            "unit": "ns",
            "range": "± 78028.42495140202"
          },
          {
            "name": "IntSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1670196.2586206896,
            "unit": "ns",
            "range": "± 16018.24459886104"
          },
          {
            "name": "LongDictionaryBenchmark.Dictionary_Remove(ItemCount: 100000)",
            "value": 1999673.725,
            "unit": "ns",
            "range": "± 53538.20636033122"
          },
          {
            "name": "LongSetBenchmark.HashSet_Remove(ItemCount: 100000)",
            "value": 1943251.9464285714,
            "unit": "ns",
            "range": "± 17164.26218939466"
          },
          {
            "name": "CelerityDictionaryBenchmark.CelerityDictionary_Remove(ItemCount: 100000)",
            "value": 1745144.2388059702,
            "unit": "ns",
            "range": "± 58129.67711676785"
          },
          {
            "name": "CeleritySetBenchmark.CeleritySet_Remove(ItemCount: 100000)",
            "value": 1422808.6692307692,
            "unit": "ns",
            "range": "± 66346.27102241441"
          },
          {
            "name": "IntDictionaryBenchmark.IntDictionary_Remove(ItemCount: 100000)",
            "value": 1878384.7073170731,
            "unit": "ns",
            "range": "± 152625.66821796814"
          },
          {
            "name": "IntSetBenchmark.IntSet_Remove(ItemCount: 100000)",
            "value": 1439941.5689655172,
            "unit": "ns",
            "range": "± 15478.467942091671"
          },
          {
            "name": "LongDictionaryBenchmark.LongDictionary_Remove(ItemCount: 100000)",
            "value": 1931463.2604790418,
            "unit": "ns",
            "range": "± 198888.46809017248"
          },
          {
            "name": "LongSetBenchmark.LongSet_Remove(ItemCount: 100000)",
            "value": 1573246.2315789473,
            "unit": "ns",
            "range": "± 62290.62584971347"
          }
        ]
      }
    ]
  }
}