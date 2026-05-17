window.BENCHMARK_DATA = {
  "lastUpdate": 1779054425722,
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
      }
    ]
  }
}