window.BENCHMARK_DATA = {
  "lastUpdate": 1779550050471,
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
      }
    ]
  }
}