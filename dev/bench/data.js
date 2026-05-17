window.BENCHMARK_DATA = {
  "lastUpdate": 1779006059291,
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
      }
    ]
  }
}