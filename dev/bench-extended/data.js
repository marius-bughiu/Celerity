window.BENCHMARK_DATA = {
  "lastUpdate": 1780761077177,
  "repoUrl": "https://github.com/marius-bughiu/Celerity",
  "entries": {
    "Celerity Extended Benchmarks": [
      {
        "commit": {
          "author": {
            "name": "Marius Bughiu",
            "username": "marius-bughiu",
            "email": "marius.bughiu@gmail.com"
          },
          "committer": {
            "name": "GitHub",
            "username": "web-flow",
            "email": "noreply@github.com"
          },
          "id": "f8386e7d25d8afd144a95f7227703ce042869e1c",
          "message": "Merge pull request #164 from marius-bughiu/feat/issue-26-benchmarks\n\nfeat(benchmarks): comprehensive benchmark suite expansion (#26)",
          "timestamp": "2026-06-06T15:10:22Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/f8386e7d25d8afd144a95f7227703ce042869e1c"
        },
        "date": 1780761075976,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1662080.9751953124,
            "unit": "ns",
            "range": "± 11478.19038201496"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 650242.7486132813,
            "unit": "ns",
            "range": "± 76462.88137951063"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1827509.9584635417,
            "unit": "ns",
            "range": "± 17972.581163129547"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2469790.2600446427,
            "unit": "ns",
            "range": "± 14361.096616574554"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1189222.9711216518,
            "unit": "ns",
            "range": "± 8607.057343797593"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2618751.9442708334,
            "unit": "ns",
            "range": "± 10479.56831038602"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4962143.214583334,
            "unit": "ns",
            "range": "± 63793.13858226111"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2310218.145228795,
            "unit": "ns",
            "range": "± 64609.20146831806"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5298103.389583333,
            "unit": "ns",
            "range": "± 54691.60069364646"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 3628427.047286184,
            "unit": "ns",
            "range": "± 79819.52837088305"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4607091.508081896,
            "unit": "ns",
            "range": "± 106260.50902893937"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 4586680.751166045,
            "unit": "ns",
            "range": "± 72590.67692494753"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4635444.602864583,
            "unit": "ns",
            "range": "± 3690.4814568320558"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1971456.7947916666,
            "unit": "ns",
            "range": "± 952.7643961550302"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13247.026523335775,
            "unit": "ns",
            "range": "± 148.09115847907594"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 9106.552680460612,
            "unit": "ns",
            "range": "± 160.65413222206655"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4126213.621354167,
            "unit": "ns",
            "range": "± 60434.58041645211"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 5065895.5828125,
            "unit": "ns",
            "range": "± 47242.75273077371"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 14619.472259521484,
            "unit": "ns",
            "range": "± 155.61916628064975"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6064.298086278579,
            "unit": "ns",
            "range": "± 116.16734318016066"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3181719.3470394737,
            "unit": "ns",
            "range": "± 182229.11238708935"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2109807.550998264,
            "unit": "ns",
            "range": "± 69756.51354349399"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 13514.485171726772,
            "unit": "ns",
            "range": "± 221.34127167377025"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 246398.64972795759,
            "unit": "ns",
            "range": "± 158.5859447958537"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3369922.9966015625,
            "unit": "ns",
            "range": "± 237741.05854883124"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 5195709200.916667,
            "unit": "ns",
            "range": "± 3789256.9312534435"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 22767000.695,
            "unit": "ns",
            "range": "± 901890.6307205507"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 25914193.104166668,
            "unit": "ns",
            "range": "± 435412.4600899331"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 25461845.095833335,
            "unit": "ns",
            "range": "± 397304.5040896517"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 230109405.73076922,
            "unit": "ns",
            "range": "± 2248154.924805465"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 147240618.07291666,
            "unit": "ns",
            "range": "± 4509676.05703191"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 142974617.6102941,
            "unit": "ns",
            "range": "± 4542642.244868418"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 23943142.66168478,
            "unit": "ns",
            "range": "± 600345.2541760624"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 31150744.719047613,
            "unit": "ns",
            "range": "± 307503.602461681"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 254675369.5949367,
            "unit": "ns",
            "range": "± 13236205.826280871"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 165265121.42222223,
            "unit": "ns",
            "range": "± 1112758.8452087312"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4597.387961796352,
            "unit": "ns",
            "range": "± 8.159190152743339"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5022.059565030611,
            "unit": "ns",
            "range": "± 4.836355606543153"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 364160.2854701451,
            "unit": "ns",
            "range": "± 274.35502381481825"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 3268.7143371582033,
            "unit": "ns",
            "range": "± 4.799190548679595"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2792.8453594843545,
            "unit": "ns",
            "range": "± 11.808982016916913"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2292.1411719689004,
            "unit": "ns",
            "range": "± 2.7130496579836474"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2232.9541619618735,
            "unit": "ns",
            "range": "± 2.497960912056633"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 49830.06752726237,
            "unit": "ns",
            "range": "± 46.58655887099701"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 35372441.16,
            "unit": "ns",
            "range": "± 25083.539602768615"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 32683.368934044473,
            "unit": "ns",
            "range": "± 34.3810569907749"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1626963.5109375,
            "unit": "ns",
            "range": "± 3223.729125021307"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 852366.1380859375,
            "unit": "ns",
            "range": "± 5118.881338416097"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 705208.6930338541,
            "unit": "ns",
            "range": "± 1417.2326481675311"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 619169.1212239583,
            "unit": "ns",
            "range": "± 10741.484958026922"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4958.198905357947,
            "unit": "ns",
            "range": "± 4.809542479912047"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2274.6837249168984,
            "unit": "ns",
            "range": "± 3.5009465147834304"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1602606.1146763393,
            "unit": "ns",
            "range": "± 2288.3213710796445"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 796715.8911458333,
            "unit": "ns",
            "range": "± 14631.283124610243"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4599.064023971558,
            "unit": "ns",
            "range": "± 3.8772823072011824"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2328.17369897025,
            "unit": "ns",
            "range": "± 1.189904368602839"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 524629.635141226,
            "unit": "ns",
            "range": "± 850.2382056019416"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 201499.41169621394,
            "unit": "ns",
            "range": "± 476.3969678534141"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4663.01980649508,
            "unit": "ns",
            "range": "± 5.483485721368076"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 78503.71216837566,
            "unit": "ns",
            "range": "± 236.57667841687106"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 589463.2619628906,
            "unit": "ns",
            "range": "± 550.3794712456016"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2264045392,
            "unit": "ns",
            "range": "± 1820236.2273444987"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 18954571.890625,
            "unit": "ns",
            "range": "± 434799.38690156746"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 10744144.109375,
            "unit": "ns",
            "range": "± 200536.11322344613"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 10358447.970833333,
            "unit": "ns",
            "range": "± 145685.94170324985"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 215646513.54497355,
            "unit": "ns",
            "range": "± 9854045.93101793"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 139797730.5,
            "unit": "ns",
            "range": "± 4541784.260646381"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 142319537.5182927,
            "unit": "ns",
            "range": "± 5125270.227482205"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 27122246.7325,
            "unit": "ns",
            "range": "± 4515730.623850088"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 21844247.749375,
            "unit": "ns",
            "range": "± 1734662.5107291231"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 254797332.8181818,
            "unit": "ns",
            "range": "± 6252545.770026751"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 170334254.45338982,
            "unit": "ns",
            "range": "± 7504814.209653428"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1881633.073939732,
            "unit": "ns",
            "range": "± 10966.15077202648"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 3706677.8140625,
            "unit": "ns",
            "range": "± 23669.70021515447"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 3739451.2901041666,
            "unit": "ns",
            "range": "± 12880.87401242327"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 17513434.18546875,
            "unit": "ns",
            "range": "± 4820785.293449781"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3625692.366887019,
            "unit": "ns",
            "range": "± 14098.546587275658"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13823138.782608695,
            "unit": "ns",
            "range": "± 349641.41867749905"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 6236909.941860465,
            "unit": "ns",
            "range": "± 166201.8882266674"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 6264652.5322580645,
            "unit": "ns",
            "range": "± 178387.65879052732"
          }
        ]
      }
    ]
  }
}