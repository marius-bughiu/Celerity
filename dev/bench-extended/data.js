window.BENCHMARK_DATA = {
  "lastUpdate": 1783938373223,
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
      },
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
          "id": "ad1c354ba381a8c1d6f43f5bbbdb37e5b2ce4f40",
          "message": "Merge pull request #205 from marius-bughiu/feat/issue-193-varint-codec\n\nfeat(primitives): add VarInt span-based LEB128 + zig-zag codec (#193)",
          "timestamp": "2026-06-07T21:15:18Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/ad1c354ba381a8c1d6f43f5bbbdb37e5b2ce4f40"
        },
        "date": 1780909139138,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1698015.5152064732,
            "unit": "ns",
            "range": "± 7431.3821650072805"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 746322.560546875,
            "unit": "ns",
            "range": "± 16869.123633341995"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1781249.7692708333,
            "unit": "ns",
            "range": "± 10548.643373324969"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2391845.6866629464,
            "unit": "ns",
            "range": "± 15838.445696928382"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1098038.1056941105,
            "unit": "ns",
            "range": "± 15307.20174863825"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2519230.7756696427,
            "unit": "ns",
            "range": "± 17980.287732933055"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4872821.398995535,
            "unit": "ns",
            "range": "± 22873.76971087242"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2286650.9609375,
            "unit": "ns",
            "range": "± 38892.0304900344"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5039548.209134615,
            "unit": "ns",
            "range": "± 33467.793604885075"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 74171.25440266928,
            "unit": "ns",
            "range": "± 361.7950836229799"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 28052.30338640911,
            "unit": "ns",
            "range": "± 1009.5439405477065"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7649.545985630581,
            "unit": "ns",
            "range": "± 2.780211279935793"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2975.5571306668794,
            "unit": "ns",
            "range": "± 1.41746032454817"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7647.764456612723,
            "unit": "ns",
            "range": "± 2.222637153963777"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2973.0575469970704,
            "unit": "ns",
            "range": "± 1.0860591659540706"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8918.525080362955,
            "unit": "ns",
            "range": "± 2.527671989735035"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 6423.513389587402,
            "unit": "ns",
            "range": "± 1.331121907820035"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8919.248268636067,
            "unit": "ns",
            "range": "± 2.0666713343517142"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 6429.69170652117,
            "unit": "ns",
            "range": "± 5.758738208797532"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 86890.43670184795,
            "unit": "ns",
            "range": "± 390.5457405479777"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 22248.23229777018,
            "unit": "ns",
            "range": "± 23.437095658851895"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 961885.2802634726,
            "unit": "ns",
            "range": "± 88068.28363979151"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 838150.3999348958,
            "unit": "ns",
            "range": "± 14345.9239005171"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 827497.3204427083,
            "unit": "ns",
            "range": "± 11632.394769971035"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 4627397.621171875,
            "unit": "ns",
            "range": "± 829339.5566655844"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4892262.506722384,
            "unit": "ns",
            "range": "± 179335.5947013435"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 5013197.252744933,
            "unit": "ns",
            "range": "± 166131.2462994718"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4395285.941666666,
            "unit": "ns",
            "range": "± 7357.2253114888945"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1903344.2494419643,
            "unit": "ns",
            "range": "± 3684.1933034796934"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13145.113395690918,
            "unit": "ns",
            "range": "± 178.19368033505376"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6592.049271174839,
            "unit": "ns",
            "range": "± 21.230797043022438"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 10982.028393554687,
            "unit": "ns",
            "range": "± 163.21728711846373"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 5769.345618184408,
            "unit": "ns",
            "range": "± 64.9776872320856"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 5729.97899661847,
            "unit": "ns",
            "range": "± 256.88745365905885"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7138.783111572266,
            "unit": "ns",
            "range": "± 228.26070163541175"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6965.561354637146,
            "unit": "ns",
            "range": "± 178.6127143600254"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 156016.69607747396,
            "unit": "ns",
            "range": "± 952.1356840462871"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 37705.6078125,
            "unit": "ns",
            "range": "± 189.01886682269858"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 37180.758165631974,
            "unit": "ns",
            "range": "± 44.02132323458746"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 43339.99774605887,
            "unit": "ns",
            "range": "± 122.78178159516452"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 41321.88318307059,
            "unit": "ns",
            "range": "± 37.71669099654899"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 3952764.46875,
            "unit": "ns",
            "range": "± 48534.10729494832"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4767079.430245535,
            "unit": "ns",
            "range": "± 131451.4648810328"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 12596.022948128837,
            "unit": "ns",
            "range": "± 213.32596447807708"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6471.240548706055,
            "unit": "ns",
            "range": "± 19.726560914553314"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 9068.634656633649,
            "unit": "ns",
            "range": "± 14.646418883266348"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4517.601438395182,
            "unit": "ns",
            "range": "± 51.0371514852234"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4945.363375345866,
            "unit": "ns",
            "range": "± 81.31017718351622"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7188.79413655599,
            "unit": "ns",
            "range": "± 126.79994383483692"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6581.097216886633,
            "unit": "ns",
            "range": "± 354.26891384534736"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 138151.88497721354,
            "unit": "ns",
            "range": "± 545.2401690582458"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 28569.62456839425,
            "unit": "ns",
            "range": "± 398.1202964540172"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 31258.86381648137,
            "unit": "ns",
            "range": "± 372.41515405944716"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 44377.48575236003,
            "unit": "ns",
            "range": "± 804.8511540698928"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 42150.61755777995,
            "unit": "ns",
            "range": "± 479.1320873161745"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3211160.3347265627,
            "unit": "ns",
            "range": "± 330809.41304029076"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2106879.0945991846,
            "unit": "ns",
            "range": "± 100593.56875848731"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 13441.240970066616,
            "unit": "ns",
            "range": "± 207.98121774107358"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6544.52740651911,
            "unit": "ns",
            "range": "± 133.19874996237664"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 228360.38258713944,
            "unit": "ns",
            "range": "± 395.79834315028637"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 475132.3236328125,
            "unit": "ns",
            "range": "± 776.0952198284261"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 164425.91108049665,
            "unit": "ns",
            "range": "± 273.6201174733222"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6974.237499237061,
            "unit": "ns",
            "range": "± 56.577845898140694"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7827.212668126943,
            "unit": "ns",
            "range": "± 310.7260824148618"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 139725.38551548548,
            "unit": "ns",
            "range": "± 704.6522044769152"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 30610938.25223214,
            "unit": "ns",
            "range": "± 18523.5575985038"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 721577.8736478365,
            "unit": "ns",
            "range": "± 322.16786446701565"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 46346.396215820314,
            "unit": "ns",
            "range": "± 450.027319498253"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 45414.2477722168,
            "unit": "ns",
            "range": "± 628.0790411463827"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3505403.61921875,
            "unit": "ns",
            "range": "± 332624.0392411571"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 4601517180.142858,
            "unit": "ns",
            "range": "± 997201.7395079446"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7064.41650797526,
            "unit": "ns",
            "range": "± 55.82248822250584"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 5868.4317443523005,
            "unit": "ns",
            "range": "± 227.18779529292996"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 516318.00667317706,
            "unit": "ns",
            "range": "± 345.8565050241848"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 9368.0059038798,
            "unit": "ns",
            "range": "± 237.00838558779833"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7743.534676208496,
            "unit": "ns",
            "range": "± 307.7681957143345"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 140943.15393066406,
            "unit": "ns",
            "range": "± 1470.4529617176786"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 29967.824236043296,
            "unit": "ns",
            "range": "± 331.83241537512987"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31272236.235576924,
            "unit": "ns",
            "range": "± 4344.827704973749"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 45797.336134847006,
            "unit": "ns",
            "range": "± 389.35091120046525"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 43899.57866821289,
            "unit": "ns",
            "range": "± 466.1551160150644"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 19694961.104166668,
            "unit": "ns",
            "range": "± 233290.8438754095"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 23152546.212384257,
            "unit": "ns",
            "range": "± 636422.299168026"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 23212732.670138888,
            "unit": "ns",
            "range": "± 466573.46714277135"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 207594093.9722222,
            "unit": "ns",
            "range": "± 1908593.775129715"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 137443876.125,
            "unit": "ns",
            "range": "± 2663650.0532716974"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 131420964.17948718,
            "unit": "ns",
            "range": "± 1752393.0801024851"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 20786709.890625,
            "unit": "ns",
            "range": "± 118674.48538769242"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 30156430.76785714,
            "unit": "ns",
            "range": "± 339264.4320150018"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 241218491.01612905,
            "unit": "ns",
            "range": "± 13631835.159762794"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 152410941.43112245,
            "unit": "ns",
            "range": "± 11135521.667476792"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4372.098420950083,
            "unit": "ns",
            "range": "± 2.2769478369092666"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4879.755931309292,
            "unit": "ns",
            "range": "± 21.81330170532965"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 322527.32220052084,
            "unit": "ns",
            "range": "± 223.79399063059086"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 2996.8684047063193,
            "unit": "ns",
            "range": "± 2.880824749030898"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2654.5491864522296,
            "unit": "ns",
            "range": "± 5.217369577815008"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2291.7136957804364,
            "unit": "ns",
            "range": "± 6.660646494347582"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2185.2254984929014,
            "unit": "ns",
            "range": "± 4.699664099750971"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 44497.28006388347,
            "unit": "ns",
            "range": "± 16.050450905718254"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 31302648.605769232,
            "unit": "ns",
            "range": "± 29842.144333020515"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 33333.259103628305,
            "unit": "ns",
            "range": "± 89.04946605324706"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1524198.3950737847,
            "unit": "ns",
            "range": "± 11492.863057400818"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 945624.3824869791,
            "unit": "ns",
            "range": "± 496.7887086985028"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 750987.1422991072,
            "unit": "ns",
            "range": "± 1202.357716363662"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 703411.9392277644,
            "unit": "ns",
            "range": "± 1091.4103750127647"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4749.349686177572,
            "unit": "ns",
            "range": "± 19.065089767273246"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2173.272958315336,
            "unit": "ns",
            "range": "± 2.8667323719492668"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4751.512290409633,
            "unit": "ns",
            "range": "± 23.201719581919818"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2034.7740617479596,
            "unit": "ns",
            "range": "± 4.649653702058543"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2292.3708979288735,
            "unit": "ns",
            "range": "± 4.550828172919389"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3142.525746859037,
            "unit": "ns",
            "range": "± 2.7267136681798005"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 10086.911835303674,
            "unit": "ns",
            "range": "± 2.0868428473018947"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 83184.27953229632,
            "unit": "ns",
            "range": "± 202.3780762568903"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 25446.736184256417,
            "unit": "ns",
            "range": "± 84.75129025387585"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 28730.447032048152,
            "unit": "ns",
            "range": "± 158.30635855262082"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 37432.820255824496,
            "unit": "ns",
            "range": "± 136.63289093112087"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 33209.010572160994,
            "unit": "ns",
            "range": "± 185.26044981535713"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1625518.0951021635,
            "unit": "ns",
            "range": "± 1575.2196511358454"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 682858.8358623798,
            "unit": "ns",
            "range": "± 861.3294722188122"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4372.173082351685,
            "unit": "ns",
            "range": "± 2.030162048120755"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1879.0258714358013,
            "unit": "ns",
            "range": "± 0.5201405583869937"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4371.871567862375,
            "unit": "ns",
            "range": "± 2.2253121736937347"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1879.7121361323766,
            "unit": "ns",
            "range": "± 1.7971155822774576"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1891.333755493164,
            "unit": "ns",
            "range": "± 1.8337988132847274"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3164.5038637014536,
            "unit": "ns",
            "range": "± 1.8118605098321976"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2661.2687385559084,
            "unit": "ns",
            "range": "± 6.246851276178554"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 43829.31791334886,
            "unit": "ns",
            "range": "± 15.363544885530484"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 17467.699657733625,
            "unit": "ns",
            "range": "± 27.091589461651566"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 20454.294241098258,
            "unit": "ns",
            "range": "± 18.971846448318793"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 37362.947880336214,
            "unit": "ns",
            "range": "± 83.02878363120865"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 33904.325430733814,
            "unit": "ns",
            "range": "± 163.1891070131337"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 439780.8976888021,
            "unit": "ns",
            "range": "± 554.5954796913812"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 189134.95340401787,
            "unit": "ns",
            "range": "± 261.1685804887359"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4416.171985332782,
            "unit": "ns",
            "range": "± 4.7919865803384765"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73400.69462890625,
            "unit": "ns",
            "range": "± 429.58995646241976"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4416.09973526001,
            "unit": "ns",
            "range": "± 2.0917772374414447"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 297782.4001464844,
            "unit": "ns",
            "range": "± 120.00153512916526"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73377.2250366211,
            "unit": "ns",
            "range": "± 131.5474329796046"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3111.3493888854982,
            "unit": "ns",
            "range": "± 5.636797084228545"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2698.2136647360667,
            "unit": "ns",
            "range": "± 2.6776637518217155"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 44801.88983154297,
            "unit": "ns",
            "range": "± 82.39590164832273"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 30612585.875,
            "unit": "ns",
            "range": "± 17381.35871312584"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 710045.6715959822,
            "unit": "ns",
            "range": "± 327.8035780088269"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 37229.253474644254,
            "unit": "ns",
            "range": "± 168.91033527356757"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 33890.922903878345,
            "unit": "ns",
            "range": "± 154.25420344942518"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 486207.86591045675,
            "unit": "ns",
            "range": "± 1086.509707707552"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2005646318.5,
            "unit": "ns",
            "range": "± 1210908.7283598057"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4370.684829076131,
            "unit": "ns",
            "range": "± 2.0529029597260333"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 1897.3922282854717,
            "unit": "ns",
            "range": "± 1.2168314436306922"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 322564.3844275841,
            "unit": "ns",
            "range": "± 81.0316567262741"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3279.4976381574356,
            "unit": "ns",
            "range": "± 2.1395833051977067"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2584.9013554499697,
            "unit": "ns",
            "range": "± 5.060720003633524"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44547.6061726888,
            "unit": "ns",
            "range": "± 22.234888582045272"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 18787.528435340293,
            "unit": "ns",
            "range": "± 17.762160045392843"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31315541.848214287,
            "unit": "ns",
            "range": "± 44334.37323224342"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 36872.847428541914,
            "unit": "ns",
            "range": "± 28.148471402357686"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 33280.45343017578,
            "unit": "ns",
            "range": "± 66.52918442392355"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 15765382.205357144,
            "unit": "ns",
            "range": "± 15158.8851131698"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 8437836.4609375,
            "unit": "ns",
            "range": "± 9535.792225044303"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 8389646.493229168,
            "unit": "ns",
            "range": "± 93197.70787966806"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 207769851.96542552,
            "unit": "ns",
            "range": "± 11776750.599428829"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 126452988.03846154,
            "unit": "ns",
            "range": "± 1778520.151924065"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 130818327.6423077,
            "unit": "ns",
            "range": "± 6090450.825101756"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 17769549.765625,
            "unit": "ns",
            "range": "± 240806.2760411673"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 14122504.9509375,
            "unit": "ns",
            "range": "± 1172243.7510207614"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 216143010.9964912,
            "unit": "ns",
            "range": "± 13473700.36867441"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 146620361.29019606,
            "unit": "ns",
            "range": "± 7362080.906736042"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7646.446432495117,
            "unit": "ns",
            "range": "± 1.903710902921985"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2570.1686717442103,
            "unit": "ns",
            "range": "± 1.3847996190004819"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7647.261480058943,
            "unit": "ns",
            "range": "± 3.087185822993492"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2583.395639038086,
            "unit": "ns",
            "range": "± 9.06156105979958"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8919.887413533528,
            "unit": "ns",
            "range": "± 2.606075076960139"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 9505.499111175537,
            "unit": "ns",
            "range": "± 6.669023526624719"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8919.531934102377,
            "unit": "ns",
            "range": "± 1.1896671823794949"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 9455.433335440499,
            "unit": "ns",
            "range": "± 8.694764349660211"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 34939.44237409319,
            "unit": "ns",
            "range": "± 22.750083581449744"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 20390.574330256535,
            "unit": "ns",
            "range": "± 27.341877137443035"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 13687.867599487305,
            "unit": "ns",
            "range": "± 154.35840404057507"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 8094.1904220581055,
            "unit": "ns",
            "range": "± 8.259958976933424"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 6378.862778799875,
            "unit": "ns",
            "range": "± 1.785316378772418"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 6391.208702087402,
            "unit": "ns",
            "range": "± 6.589103621960585"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 12905.402902875629,
            "unit": "ns",
            "range": "± 30.847322384711546"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 33715.27336472731,
            "unit": "ns",
            "range": "± 92.78804944769222"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 26084.8983001709,
            "unit": "ns",
            "range": "± 24.975090257031493"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 15001.229221598307,
            "unit": "ns",
            "range": "± 2.6890198917627024"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 8951.946200779506,
            "unit": "ns",
            "range": "± 9.036911821609303"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5120.805868002085,
            "unit": "ns",
            "range": "± 5.3044706743620695"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5124.2616855076385,
            "unit": "ns",
            "range": "± 7.036523707005221"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 12160.439877101353,
            "unit": "ns",
            "range": "± 8.887054511785097"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 97255.7974155971,
            "unit": "ns",
            "range": "± 65.16556682858392"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 19848.70739440918,
            "unit": "ns",
            "range": "± 34.54540300027055"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 13417.880003865559,
            "unit": "ns",
            "range": "± 3.505671396015912"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 9155.936289273775,
            "unit": "ns",
            "range": "± 5.748141925286281"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4251.6743730817525,
            "unit": "ns",
            "range": "± 4.642559830753929"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4099.490589396159,
            "unit": "ns",
            "range": "± 0.8898591668966971"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 11472.25660588191,
            "unit": "ns",
            "range": "± 6.033923682726082"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1904583.6868489583,
            "unit": "ns",
            "range": "± 31318.575773607852"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 2942729.0091145835,
            "unit": "ns",
            "range": "± 61202.60005841667"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2875507.9876802885,
            "unit": "ns",
            "range": "± 28646.766913283496"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 10268830.474479167,
            "unit": "ns",
            "range": "± 24209.097543953972"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3357560.076171875,
            "unit": "ns",
            "range": "± 5600.005490588704"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13647526.203703703,
            "unit": "ns",
            "range": "± 369385.48866543354"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5806570.74137931,
            "unit": "ns",
            "range": "± 146083.60251777846"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5858968.567567567,
            "unit": "ns",
            "range": "± 140942.09895783852"
          }
        ]
      },
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
          "id": "2695eb3631c14f1b03ef8781520d51f0b181cfc3",
          "message": "Merge pull request #214 from marius-bughiu/feat/issue-190-release-pipeline\n\nfeat(infra): symbol packages, SourceLink & deterministic release pipeline (#190)",
          "timestamp": "2026-06-15T06:47:21Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/2695eb3631c14f1b03ef8781520d51f0b181cfc3"
        },
        "date": 1781515485488,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1024)",
            "value": 370.27939800115735,
            "unit": "ns",
            "range": "± 0.2876013059015659"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1024)",
            "value": 32.997434501846634,
            "unit": "ns",
            "range": "± 0.03151559853262442"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1024)",
            "value": 100.09231912631255,
            "unit": "ns",
            "range": "± 0.07238485221775236"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1000000)",
            "value": 354413.39216496394,
            "unit": "ns",
            "range": "± 540.7898707017055"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1000000)",
            "value": 54347.95570725661,
            "unit": "ns",
            "range": "± 46.727828920503754"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1000000)",
            "value": 96305.6760911208,
            "unit": "ns",
            "range": "± 51.930292356882966"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1667541.584375,
            "unit": "ns",
            "range": "± 11907.586975697619"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 788590.6945638021,
            "unit": "ns",
            "range": "± 12015.338042708941"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1896905.3364583333,
            "unit": "ns",
            "range": "± 12536.411115989311"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2487278.4171316964,
            "unit": "ns",
            "range": "± 19841.695488733683"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1190566.3087439905,
            "unit": "ns",
            "range": "± 5763.872037453463"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2715589.56171875,
            "unit": "ns",
            "range": "± 28870.52384758217"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5035924.2796875,
            "unit": "ns",
            "range": "± 57313.80269179078"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2192510.5783547796,
            "unit": "ns",
            "range": "± 34340.09693221119"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5422357.202636719,
            "unit": "ns",
            "range": "± 105667.98915716095"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 67174.03755405972,
            "unit": "ns",
            "range": "± 1025.5526408979947"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 22611.130076090496,
            "unit": "ns",
            "range": "± 45.25332544332014"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_NaiveLoop",
            "value": 23097.220581054688,
            "unit": "ns",
            "range": "± 8.113778779327852"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_MathLog10",
            "value": 38738.14863469051,
            "unit": "ns",
            "range": "± 17.969859967367107"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_FastUtils",
            "value": 3704.567014694214,
            "unit": "ns",
            "range": "± 2.3417898253313596"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_NaiveLoop",
            "value": 61554.93110069862,
            "unit": "ns",
            "range": "± 156.08233787896768"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_MathLog10",
            "value": 39474.71665837215,
            "unit": "ns",
            "range": "± 37.12539616386342"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_FastUtils",
            "value": 8243.958078055546,
            "unit": "ns",
            "range": "± 359.8308501510138"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8637.96875406901,
            "unit": "ns",
            "range": "± 1.3903506423964496"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 3375.945953641619,
            "unit": "ns",
            "range": "± 0.43029213237516334"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8636.989092508951,
            "unit": "ns",
            "range": "± 1.0781227435776888"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 3378.480661937169,
            "unit": "ns",
            "range": "± 0.9522455884751179"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 10082.878591684195,
            "unit": "ns",
            "range": "± 6.655862689997188"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7233.851162719727,
            "unit": "ns",
            "range": "± 3.2706419031132605"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 10077.519150954027,
            "unit": "ns",
            "range": "± 0.8573929735797826"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7235.917556175818,
            "unit": "ns",
            "range": "± 6.396507870373348"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 88760.31051199777,
            "unit": "ns",
            "range": "± 34.13925991117339"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 16016.168655395508,
            "unit": "ns",
            "range": "± 12.222721262799817"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 976421.8779496173,
            "unit": "ns",
            "range": "± 89416.0107192363"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 877175.8784555289,
            "unit": "ns",
            "range": "± 7121.568462019477"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 883579.8704427084,
            "unit": "ns",
            "range": "± 9829.201377859305"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 5699301.2921875,
            "unit": "ns",
            "range": "± 55510.76233496085"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 5453020.684244792,
            "unit": "ns",
            "range": "± 16784.73633871954"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 5407171.140625,
            "unit": "ns",
            "range": "± 50276.025126685665"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4621128.886418269,
            "unit": "ns",
            "range": "± 5998.594266120305"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1992598.6361177885,
            "unit": "ns",
            "range": "± 1435.1323786320959"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13206.598042414738,
            "unit": "ns",
            "range": "± 61.77428438551436"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7380.665546162923,
            "unit": "ns",
            "range": "± 121.4398845975558"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 8541.279165649414,
            "unit": "ns",
            "range": "± 222.91744923021193"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 5855.294708985549,
            "unit": "ns",
            "range": "± 239.127845854233"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6127.919968741281,
            "unit": "ns",
            "range": "± 140.4327032319328"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 8092.990783691406,
            "unit": "ns",
            "range": "± 90.64882313373379"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7313.5539860444915,
            "unit": "ns",
            "range": "± 149.40431768059452"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 166814.00480143228,
            "unit": "ns",
            "range": "± 1947.9775440626308"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 34029.095306396484,
            "unit": "ns",
            "range": "± 286.1298906612492"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 35837.763083321704,
            "unit": "ns",
            "range": "± 121.50958832337604"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 45857.56778971354,
            "unit": "ns",
            "range": "± 767.0465382454964"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 41349.11603655134,
            "unit": "ns",
            "range": "± 258.3123668402992"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4208884.265625,
            "unit": "ns",
            "range": "± 71282.93481893117"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 5083020.190104167,
            "unit": "ns",
            "range": "± 35596.088978001375"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 15100.514913385565,
            "unit": "ns",
            "range": "± 366.03004772998395"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 10246.823807660272,
            "unit": "ns",
            "range": "± 209.31887388171825"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6048.062514476287,
            "unit": "ns",
            "range": "± 211.23686103531827"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4289.417505900065,
            "unit": "ns",
            "range": "± 29.230172603197413"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4613.54672648112,
            "unit": "ns",
            "range": "± 56.05032763287033"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7525.46083014352,
            "unit": "ns",
            "range": "± 19.235666333324403"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6663.962162017822,
            "unit": "ns",
            "range": "± 64.76391239530578"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 155136.16428920202,
            "unit": "ns",
            "range": "± 695.0327783018514"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 25398.903477260046,
            "unit": "ns",
            "range": "± 154.20275245667676"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 25929.89563700358,
            "unit": "ns",
            "range": "± 236.82740132785958"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 45743.069108072916,
            "unit": "ns",
            "range": "± 196.04999853125645"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 41510.440608723955,
            "unit": "ns",
            "range": "± 503.9554637909168"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3134295.7489983975,
            "unit": "ns",
            "range": "± 154547.3231083502"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2140016.338005515,
            "unit": "ns",
            "range": "± 41950.316063532366"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 13185.263365609306,
            "unit": "ns",
            "range": "± 69.69693887967355"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 8822.218597920735,
            "unit": "ns",
            "range": "± 88.6386453042219"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 247089.28327287946,
            "unit": "ns",
            "range": "± 278.8266471901974"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 528579.6829427084,
            "unit": "ns",
            "range": "± 109.54636563675626"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 178020.07212611608,
            "unit": "ns",
            "range": "± 135.3691886818429"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7970.109732994666,
            "unit": "ns",
            "range": "± 208.82928711839"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6696.46142171224,
            "unit": "ns",
            "range": "± 90.86504857225128"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 153276.21240234375,
            "unit": "ns",
            "range": "± 514.8174446357935"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 34562051.569230765,
            "unit": "ns",
            "range": "± 13691.242686872485"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 766364.1571614583,
            "unit": "ns",
            "range": "± 1098.098035722259"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 44855.19083077567,
            "unit": "ns",
            "range": "± 229.04008071477676"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 40177.33353169759,
            "unit": "ns",
            "range": "± 53.103664049457166"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3350617.025749362,
            "unit": "ns",
            "range": "± 257864.7741079106"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 5196452073.466666,
            "unit": "ns",
            "range": "± 1353800.2011910698"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 9335.063698323567,
            "unit": "ns",
            "range": "± 88.81163714837366"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4406.032433573405,
            "unit": "ns",
            "range": "± 32.73648778839398"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 581206.953125,
            "unit": "ns",
            "range": "± 400.0520349935146"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7819.318183898926,
            "unit": "ns",
            "range": "± 37.378951175694674"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6484.100030008952,
            "unit": "ns",
            "range": "± 19.752566109927056"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 151709.03321940106,
            "unit": "ns",
            "range": "± 564.2651865211235"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 25224.30369466146,
            "unit": "ns",
            "range": "± 122.5571156921265"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 35323584.58974359,
            "unit": "ns",
            "range": "± 3245.4236134904545"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44763.04407552083,
            "unit": "ns",
            "range": "± 109.65069309842767"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 39863.433393205916,
            "unit": "ns",
            "range": "± 97.02993367084831"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 24445666.204066265,
            "unit": "ns",
            "range": "± 1294713.7252563199"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 25759919.13839286,
            "unit": "ns",
            "range": "± 126271.46818029265"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 26239844.8125,
            "unit": "ns",
            "range": "± 481002.77266683406"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 260884807.76612905,
            "unit": "ns",
            "range": "± 10661665.778288048"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 162611077.08333334,
            "unit": "ns",
            "range": "± 5028273.3422745485"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 161075234.94871795,
            "unit": "ns",
            "range": "± 4401748.30272589"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 28457814.23800505,
            "unit": "ns",
            "range": "± 1907229.095962818"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 34016550.395833336,
            "unit": "ns",
            "range": "± 581441.3239187874"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 297702330.5555556,
            "unit": "ns",
            "range": "± 5589996.079281577"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 180832162.57971016,
            "unit": "ns",
            "range": "± 4569201.783067179"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4597.608177185059,
            "unit": "ns",
            "range": "± 2.101160980107913"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 5339.703144618443,
            "unit": "ns",
            "range": "± 5.527118407763982"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 364347.11696213944,
            "unit": "ns",
            "range": "± 232.8327604231891"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 3275.734073093959,
            "unit": "ns",
            "range": "± 1.8755914894629213"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2797.082447932317,
            "unit": "ns",
            "range": "± 4.411288249395746"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2281.599218913487,
            "unit": "ns",
            "range": "± 2.495584780799987"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2246.3463542644795,
            "unit": "ns",
            "range": "± 2.4798666955390183"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 49239.4052734375,
            "unit": "ns",
            "range": "± 32.06251277735104"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 35435519.284444444,
            "unit": "ns",
            "range": "± 51993.164146230345"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 32855.34245417668,
            "unit": "ns",
            "range": "± 18.326256544359996"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1633428.080078125,
            "unit": "ns",
            "range": "± 1444.0564749487687"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 916325.1530949519,
            "unit": "ns",
            "range": "± 1770.0437785555498"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 676877.7854817709,
            "unit": "ns",
            "range": "± 10908.292293056578"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 637416.1499023438,
            "unit": "ns",
            "range": "± 12053.422673398783"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4965.251482009888,
            "unit": "ns",
            "range": "± 4.145902791465174"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 13318.10092485868,
            "unit": "ns",
            "range": "± 3.5372718118298354"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4957.221730158879,
            "unit": "ns",
            "range": "± 2.016647153020696"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2187.1069992610387,
            "unit": "ns",
            "range": "± 3.349771504196336"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2292.2054781595866,
            "unit": "ns",
            "range": "± 5.1945095914580985"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3380.77955754598,
            "unit": "ns",
            "range": "± 1.185859815384633"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2867.308784212385,
            "unit": "ns",
            "range": "± 2.124586796702645"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 55987.71696573893,
            "unit": "ns",
            "range": "± 74.93499247669813"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 26303.731120518274,
            "unit": "ns",
            "range": "± 66.61465169561228"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 27463.390692138673,
            "unit": "ns",
            "range": "± 16.433357205015934"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 38337.51728233924,
            "unit": "ns",
            "range": "± 19.888326611460794"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 38607.8333791097,
            "unit": "ns",
            "range": "± 13.43564085001143"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1543713.6477864583,
            "unit": "ns",
            "range": "± 55592.38056823172"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 786999.9138532366,
            "unit": "ns",
            "range": "± 3691.502828105832"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4595.696143468221,
            "unit": "ns",
            "range": "± 2.191560152837515"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2003.6020305340107,
            "unit": "ns",
            "range": "± 4.231476894537379"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4596.515358516148,
            "unit": "ns",
            "range": "± 1.903937569658997"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1886.0632640838624,
            "unit": "ns",
            "range": "± 2.190630425873224"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1960.7737027681792,
            "unit": "ns",
            "range": "± 3.2550255929467653"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3419.5082193102157,
            "unit": "ns",
            "range": "± 3.2128074728076705"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2861.5847633906774,
            "unit": "ns",
            "range": "± 3.4942880515065555"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 46069.25391060965,
            "unit": "ns",
            "range": "± 32.76406928647145"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18698.501562935966,
            "unit": "ns",
            "range": "± 20.708816409039322"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 19686.348212608926,
            "unit": "ns",
            "range": "± 24.96574296054013"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 38303.424978402945,
            "unit": "ns",
            "range": "± 25.4701917199525"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 33560.531482403094,
            "unit": "ns",
            "range": "± 27.08453996322557"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 462569.71529947914,
            "unit": "ns",
            "range": "± 544.1857277407524"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 197309.54511369977,
            "unit": "ns",
            "range": "± 388.06555854756"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4680.187200273786,
            "unit": "ns",
            "range": "± 4.54745147143436"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 78741.02881798378,
            "unit": "ns",
            "range": "± 260.6738039411666"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4675.541260274252,
            "unit": "ns",
            "range": "± 10.20625287252388"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 334584.19755859376,
            "unit": "ns",
            "range": "± 579.1511989354689"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 79503.34380086263,
            "unit": "ns",
            "range": "± 106.33371790997226"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3399.3281150230996,
            "unit": "ns",
            "range": "± 2.2554183409899378"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2863.4306896754674,
            "unit": "ns",
            "range": "± 2.38819417430303"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 47122.78013305664,
            "unit": "ns",
            "range": "± 124.63420201978236"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 34651431.43333333,
            "unit": "ns",
            "range": "± 83529.01624293186"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 769558.6260463169,
            "unit": "ns",
            "range": "± 1426.5040345635389"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 38057.31921386719,
            "unit": "ns",
            "range": "± 26.259566171469583"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 33445.42656062199,
            "unit": "ns",
            "range": "± 21.830953071998135"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 520937.2378305289,
            "unit": "ns",
            "range": "± 505.3317860511519"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2264762323.2,
            "unit": "ns",
            "range": "± 1420007.7144150713"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4597.9996408735005,
            "unit": "ns",
            "range": "± 2.619038037149678"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 1862.936634881156,
            "unit": "ns",
            "range": "± 4.456381620386122"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 364340.23604910716,
            "unit": "ns",
            "range": "± 174.89178761969205"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3418.189403806414,
            "unit": "ns",
            "range": "± 3.667011604171858"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2787.8865036597617,
            "unit": "ns",
            "range": "± 3.700127177051667"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 49194.13843180339,
            "unit": "ns",
            "range": "± 37.99252796332543"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 18640.61641845703,
            "unit": "ns",
            "range": "± 47.00786980857252"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 35417809.333333336,
            "unit": "ns",
            "range": "± 54986.85085543809"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 38355.92129751352,
            "unit": "ns",
            "range": "± 33.144168301865456"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 32885.595529409555,
            "unit": "ns",
            "range": "± 13.731207549006413"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 22394667.430689104,
            "unit": "ns",
            "range": "± 1148637.002259657"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 12241386.224888394,
            "unit": "ns",
            "range": "± 341380.13883893256"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 11973893.434103262,
            "unit": "ns",
            "range": "± 458210.76987981866"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 251054337.90151513,
            "unit": "ns",
            "range": "± 9428266.856979735"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 167726749.95762712,
            "unit": "ns",
            "range": "± 7384311.000680368"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 154587385.07894737,
            "unit": "ns",
            "range": "± 3386750.7621916737"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 28173731.2396875,
            "unit": "ns",
            "range": "± 3086720.4511017697"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 23020111.5171875,
            "unit": "ns",
            "range": "± 1489227.2888532863"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 276054872.47619045,
            "unit": "ns",
            "range": "± 6489926.988255681"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 174330670.2352941,
            "unit": "ns",
            "range": "± 3477709.6095060934"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1024)",
            "value": 97.52115339040756,
            "unit": "ns",
            "range": "± 0.41093041841885825"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1024)",
            "value": 745.1866220474243,
            "unit": "ns",
            "range": "± 0.8033217376464026"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1024)",
            "value": 99.15145151955741,
            "unit": "ns",
            "range": "± 0.26274123224754065"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1000000)",
            "value": 123454.13351004464,
            "unit": "ns",
            "range": "± 131.473112833611"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1000000)",
            "value": 706711.8003255208,
            "unit": "ns",
            "range": "± 423.03440255736706"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1000000)",
            "value": 80137.3713704427,
            "unit": "ns",
            "range": "± 114.65738005374641"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8638.361589704242,
            "unit": "ns",
            "range": "± 2.4000257240036973"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2892.6848523276194,
            "unit": "ns",
            "range": "± 2.2456710959890014"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8638.505109514508,
            "unit": "ns",
            "range": "± 3.016102387233454"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2890.276903424944,
            "unit": "ns",
            "range": "± 1.8752835291449323"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 10081.093481881278,
            "unit": "ns",
            "range": "± 2.7646618945011006"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 10425.26220350999,
            "unit": "ns",
            "range": "± 3.4209394618404216"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 10077.91594696045,
            "unit": "ns",
            "range": "± 1.5170267539785725"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 10431.065078735352,
            "unit": "ns",
            "range": "± 5.09540291780891"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 39416.60917881557,
            "unit": "ns",
            "range": "± 14.975593303940254"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 18287.171591186525,
            "unit": "ns",
            "range": "± 35.938944909169386"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 11536.578245896559,
            "unit": "ns",
            "range": "± 6.56069152851467"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 10276.252130361703,
            "unit": "ns",
            "range": "± 3.556480345980059"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 7210.014410752517,
            "unit": "ns",
            "range": "± 2.54937431783088"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 7152.659630408654,
            "unit": "ns",
            "range": "± 6.098962156111304"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 14692.970973714193,
            "unit": "ns",
            "range": "± 28.053388485245943"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 38490.547264686,
            "unit": "ns",
            "range": "± 38.31826911779126"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 20480.16517537435,
            "unit": "ns",
            "range": "± 17.6321783968475"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 11331.753578772912,
            "unit": "ns",
            "range": "± 2.57777338513835"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 10861.355998447963,
            "unit": "ns",
            "range": "± 7.4837795848144735"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5777.334048679897,
            "unit": "ns",
            "range": "± 9.927307126966692"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5781.010400038499,
            "unit": "ns",
            "range": "± 8.64580923281702"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 13643.058165413993,
            "unit": "ns",
            "range": "± 6.813154965687764"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 104601.37915910993,
            "unit": "ns",
            "range": "± 46.37166519171522"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 18362.057686360677,
            "unit": "ns",
            "range": "± 38.883563582562324"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 10438.6257425944,
            "unit": "ns",
            "range": "± 6.404127434522559"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 10345.405670166016,
            "unit": "ns",
            "range": "± 5.131908668293605"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4631.817371368408,
            "unit": "ns",
            "range": "± 0.8741347044642219"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4674.111883980887,
            "unit": "ns",
            "range": "± 4.570079829642869"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 12973.453899676982,
            "unit": "ns",
            "range": "± 15.613314464881817"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 1024)",
            "value": 1101.2953951699394,
            "unit": "ns",
            "range": "± 12.736968173353093"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 1024)",
            "value": 7.059223686655362,
            "unit": "ns",
            "range": "± 0.03819560357611341"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 65536)",
            "value": 71442.88916015625,
            "unit": "ns",
            "range": "± 2184.3825505186205"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 65536)",
            "value": 370.73140692710876,
            "unit": "ns",
            "range": "± 0.4116436339837645"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1880261.2328814338,
            "unit": "ns",
            "range": "± 36648.525377592676"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 3040538.56328125,
            "unit": "ns",
            "range": "± 44041.44316305026"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2992965.124441964,
            "unit": "ns",
            "range": "± 23138.186646375365"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 1024)",
            "value": 1106.3565644582113,
            "unit": "ns",
            "range": "± 18.88133263767195"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 1024)",
            "value": 847.5987761570857,
            "unit": "ns",
            "range": "± 4.2329543951082265"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 65536)",
            "value": 73386.35384277343,
            "unit": "ns",
            "range": "± 1899.046585166553"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 65536)",
            "value": 53929.04504394531,
            "unit": "ns",
            "range": "± 12.86360684295158"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 1024)",
            "value": 290.23279175391565,
            "unit": "ns",
            "range": "± 0.5749456794891404"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 1024)",
            "value": 280.61696897234236,
            "unit": "ns",
            "range": "± 1.0516759166976704"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 65536)",
            "value": 17614.543053260215,
            "unit": "ns",
            "range": "± 14.386509406769543"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 65536)",
            "value": 17488.13412945087,
            "unit": "ns",
            "range": "± 11.06432938027357"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 15613691.85671875,
            "unit": "ns",
            "range": "± 1581036.5120623"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3593387.2682291665,
            "unit": "ns",
            "range": "± 22605.189119384708"
          },
          {
            "name": "GuidBenchmark.V4_BclNewGuid",
            "value": 2825584.024832589,
            "unit": "ns",
            "range": "± 5569.04202255451"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_Xoshiro",
            "value": 77942.62991768973,
            "unit": "ns",
            "range": "± 43.038192521162145"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_WyRand",
            "value": 77614.8079740084,
            "unit": "ns",
            "range": "± 60.268534587598516"
          },
          {
            "name": "GuidBenchmark.V7_BclNewGuid",
            "value": 2821412.4363839286,
            "unit": "ns",
            "range": "± 3276.379769426639"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Stateless",
            "value": 75334.84696138822,
            "unit": "ns",
            "range": "± 25.26215315439751"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Monotonic",
            "value": 72382.10196358817,
            "unit": "ns",
            "range": "± 27.809807909826194"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13994091.444444444,
            "unit": "ns",
            "range": "± 389020.12905734696"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 6312100.511627907,
            "unit": "ns",
            "range": "± 190622.85024578572"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 6399529.071428572,
            "unit": "ns",
            "range": "± 230745.2208576665"
          }
        ]
      },
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
          "id": "2936937c91778c5a165b80680421a22b5ce429f5",
          "message": "Merge pull request #227 from marius-bughiu/fix/issue-226-cms-grid-overflow\n\nfix(collections): guard CountMinSketch depth*width grid against integer overflow (#226)",
          "timestamp": "2026-06-22T06:37:27Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/2936937c91778c5a165b80680421a22b5ce429f5"
        },
        "date": 1782121038651,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1024)",
            "value": 327.47301506996155,
            "unit": "ns",
            "range": "± 0.11075976432868913"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1024)",
            "value": 32.12670629024505,
            "unit": "ns",
            "range": "± 0.19437581330233247"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1024)",
            "value": 141.86322140693665,
            "unit": "ns",
            "range": "± 0.05261239501333648"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1000000)",
            "value": 314326.42228816106,
            "unit": "ns",
            "range": "± 879.4530040034975"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1000000)",
            "value": 50404.462101862984,
            "unit": "ns",
            "range": "± 236.9438740339729"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1000000)",
            "value": 140039.81324986048,
            "unit": "ns",
            "range": "± 267.1963065899757"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1724533.2388671874,
            "unit": "ns",
            "range": "± 15157.191440261766"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 751876.2619140625,
            "unit": "ns",
            "range": "± 10186.05784728075"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1820827.0883463542,
            "unit": "ns",
            "range": "± 32600.294428120975"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2461247.135986328,
            "unit": "ns",
            "range": "± 45997.90304025557"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1168378.2895132212,
            "unit": "ns",
            "range": "± 8432.055477836824"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2567419.724609375,
            "unit": "ns",
            "range": "± 30473.65062856261"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4832021.8265625,
            "unit": "ns",
            "range": "± 41203.63523779982"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2296501.040885417,
            "unit": "ns",
            "range": "± 34713.4233024509"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5230860.687760416,
            "unit": "ns",
            "range": "± 88735.70008084326"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 74498.8450491769,
            "unit": "ns",
            "range": "± 242.05942264091547"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 27079.428142841047,
            "unit": "ns",
            "range": "± 13.481508344402165"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_NaiveLoop",
            "value": 30006.117307390487,
            "unit": "ns",
            "range": "± 26.259011892598757"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_MathLog10",
            "value": 42171.95253108098,
            "unit": "ns",
            "range": "± 20.664343381905827"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_FastUtils",
            "value": 3350.4773013775166,
            "unit": "ns",
            "range": "± 3.4039531204672318"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_NaiveLoop",
            "value": 99399.94505896934,
            "unit": "ns",
            "range": "± 65.39725653913204"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_MathLog10",
            "value": 41951.08673799955,
            "unit": "ns",
            "range": "± 17.18778256026085"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_FastUtils",
            "value": 8391.525435311454,
            "unit": "ns",
            "range": "± 5.817532340631457"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7645.484990046574,
            "unit": "ns",
            "range": "± 2.0076241220415065"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2974.716718233549,
            "unit": "ns",
            "range": "± 1.5873747482788556"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7645.914472139799,
            "unit": "ns",
            "range": "± 2.95853902630536"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2974.533115386963,
            "unit": "ns",
            "range": "± 1.0834962577476173"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8918.943614079402,
            "unit": "ns",
            "range": "± 2.8659401902236743"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 6428.161354064941,
            "unit": "ns",
            "range": "± 5.799648831544021"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8920.688507080078,
            "unit": "ns",
            "range": "± 3.670728597381565"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 6425.0517994807315,
            "unit": "ns",
            "range": "± 2.1419213263977306"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 86657.34239095052,
            "unit": "ns",
            "range": "± 411.72878583013977"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 22248.420636494953,
            "unit": "ns",
            "range": "± 22.275101173574182"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 997306.1200585938,
            "unit": "ns",
            "range": "± 130913.45751425074"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 857960.441796875,
            "unit": "ns",
            "range": "± 15498.75979033014"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 822881.1638671875,
            "unit": "ns",
            "range": "± 13449.96997935547"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 5239369.460779672,
            "unit": "ns",
            "range": "± 463610.21641081705"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4889979.357572115,
            "unit": "ns",
            "range": "± 35419.91680732134"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 4905419.9984375,
            "unit": "ns",
            "range": "± 64669.697968029366"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4396452.468191965,
            "unit": "ns",
            "range": "± 3136.800264511147"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1890224.2801983173,
            "unit": "ns",
            "range": "± 1301.1025044053185"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 14080.942701682066,
            "unit": "ns",
            "range": "± 485.4666593054169"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7132.650375843048,
            "unit": "ns",
            "range": "± 133.49889943633184"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 11576.944291085907,
            "unit": "ns",
            "range": "± 362.4345897359714"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7621.564335784912,
            "unit": "ns",
            "range": "± 665.1447958693764"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7429.762406082154,
            "unit": "ns",
            "range": "± 438.6211441905655"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 8531.36860715426,
            "unit": "ns",
            "range": "± 478.20124441979505"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7175.539610726492,
            "unit": "ns",
            "range": "± 89.54350981349212"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 157193.92799479168,
            "unit": "ns",
            "range": "± 2319.4746427139653"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 40526.3601735433,
            "unit": "ns",
            "range": "± 1009.8842668390025"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 41043.81236097548,
            "unit": "ns",
            "range": "± 844.4149926036073"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 46695.22476196289,
            "unit": "ns",
            "range": "± 760.9221767400287"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 44406.58084716797,
            "unit": "ns",
            "range": "± 603.3726203639214"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4017669.4125,
            "unit": "ns",
            "range": "± 50957.31610140742"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4872037.184151785,
            "unit": "ns",
            "range": "± 58134.253970562684"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 14162.60852686564,
            "unit": "ns",
            "range": "± 462.01062817798095"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7080.848640918732,
            "unit": "ns",
            "range": "± 216.40352208376908"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 10015.828990681966,
            "unit": "ns",
            "range": "± 159.57130126642767"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 5705.738135746547,
            "unit": "ns",
            "range": "± 69.0012028729848"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 5375.789072494506,
            "unit": "ns",
            "range": "± 331.51784961404496"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7530.820905751196,
            "unit": "ns",
            "range": "± 216.2925006905132"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6976.773005738551,
            "unit": "ns",
            "range": "± 276.40359295542936"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 138664.7887532552,
            "unit": "ns",
            "range": "± 759.1710002805291"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 32434.04042162214,
            "unit": "ns",
            "range": "± 1381.8708120372296"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 33376.85647848378,
            "unit": "ns",
            "range": "± 829.3421617012784"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 47754.48112952191,
            "unit": "ns",
            "range": "± 1175.464394649738"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 45569.91036199754,
            "unit": "ns",
            "range": "± 1380.8999486752882"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3437671.97265625,
            "unit": "ns",
            "range": "± 411101.40373501054"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2201362.234375,
            "unit": "ns",
            "range": "± 104797.07881618415"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 14731.23812979239,
            "unit": "ns",
            "range": "± 404.2708443573543"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7079.658857345581,
            "unit": "ns",
            "range": "± 247.47132201477055"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 228922.47697566106,
            "unit": "ns",
            "range": "± 210.60101245703027"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 475551.05496651784,
            "unit": "ns",
            "range": "± 813.8393030528019"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 165604.58482947716,
            "unit": "ns",
            "range": "± 201.7785403488977"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 8530.922538452149,
            "unit": "ns",
            "range": "± 341.45108255364954"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 8046.466591644287,
            "unit": "ns",
            "range": "± 525.011218299485"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 144413.420703125,
            "unit": "ns",
            "range": "± 2540.637785415679"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 30637169.26875,
            "unit": "ns",
            "range": "± 19300.612789510436"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 723396.8194754465,
            "unit": "ns",
            "range": "± 306.68521493594443"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 48306.27520751953,
            "unit": "ns",
            "range": "± 719.1144355017335"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 46780.93551097197,
            "unit": "ns",
            "range": "± 913.5567190106667"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3842697.475546875,
            "unit": "ns",
            "range": "± 350067.11544667097"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 4604862412.583333,
            "unit": "ns",
            "range": "± 2645696.5497904653"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6997.135979501824,
            "unit": "ns",
            "range": "± 239.95654245196224"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7044.512861039903,
            "unit": "ns",
            "range": "± 346.71133067896216"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 516519.4405799279,
            "unit": "ns",
            "range": "± 328.5474509646217"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 9465.097910794344,
            "unit": "ns",
            "range": "± 230.47412276226552"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7383.542663097382,
            "unit": "ns",
            "range": "± 138.79239901288352"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 149647.95405273436,
            "unit": "ns",
            "range": "± 1898.601562466382"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 34123.404871890416,
            "unit": "ns",
            "range": "± 745.9364989330919"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31299175.4375,
            "unit": "ns",
            "range": "± 20148.38625644576"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 48311.29068603516,
            "unit": "ns",
            "range": "± 482.71927460361667"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44836.06566975911,
            "unit": "ns",
            "range": "± 549.1407492543792"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 35239238.14,
            "unit": "ns",
            "range": "± 7562815.677297759"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 27462676.933894232,
            "unit": "ns",
            "range": "± 729602.5636654806"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 25518291.45707237,
            "unit": "ns",
            "range": "± 1462209.4851938111"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 338384425.8666667,
            "unit": "ns",
            "range": "± 4953911.091325964"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 159209103.4027778,
            "unit": "ns",
            "range": "± 3397827.5728383507"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 158017485.67619047,
            "unit": "ns",
            "range": "± 5006422.248147314"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 38411493.845833324,
            "unit": "ns",
            "range": "± 8494180.92860311"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 35683865.1,
            "unit": "ns",
            "range": "± 817244.9182232935"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 318013131.32,
            "unit": "ns",
            "range": "± 21058982.481968693"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 166372697.7936508,
            "unit": "ns",
            "range": "± 3950785.8620320517"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4371.039512634277,
            "unit": "ns",
            "range": "± 3.0100514584105946"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4989.393598429362,
            "unit": "ns",
            "range": "± 20.18193664141846"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 322955.11553485575,
            "unit": "ns",
            "range": "± 498.3129624757119"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 2990.3883373553936,
            "unit": "ns",
            "range": "± 3.1839858733621305"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2665.3367497580393,
            "unit": "ns",
            "range": "± 6.776001832608945"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2440.456802368164,
            "unit": "ns",
            "range": "± 5.110419251704669"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2174.102071908804,
            "unit": "ns",
            "range": "± 4.894579232380572"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 44481.02593485514,
            "unit": "ns",
            "range": "± 152.8900254842719"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 31278495.21153846,
            "unit": "ns",
            "range": "± 9465.741524006922"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 33440.93024855394,
            "unit": "ns",
            "range": "± 120.34794183770893"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1636413.6265625,
            "unit": "ns",
            "range": "± 9064.239074361667"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 972056.2241908482,
            "unit": "ns",
            "range": "± 5196.56594806578"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 726675.69140625,
            "unit": "ns",
            "range": "± 2379.3572151756835"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 715620.5583333333,
            "unit": "ns",
            "range": "± 3000.4213657943674"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4769.020771906926,
            "unit": "ns",
            "range": "± 18.371411915710862"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2162.595828129695,
            "unit": "ns",
            "range": "± 2.1036440897978586"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4782.282227736253,
            "unit": "ns",
            "range": "± 10.816595089385176"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2366.671287027995,
            "unit": "ns",
            "range": "± 9.400398254126378"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2289.5935453687393,
            "unit": "ns",
            "range": "± 7.965505070743728"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3155.6916007995605,
            "unit": "ns",
            "range": "± 6.946459646758252"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2717.8298819859824,
            "unit": "ns",
            "range": "± 2.9635233229431477"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 83183.62647356305,
            "unit": "ns",
            "range": "± 209.4616126544229"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 25663.84714449369,
            "unit": "ns",
            "range": "± 117.37495989009808"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 28943.210270472937,
            "unit": "ns",
            "range": "± 203.01698524452166"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 38633.441798618864,
            "unit": "ns",
            "range": "± 85.48974540712402"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 33349.52081298828,
            "unit": "ns",
            "range": "± 112.58168146287979"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1612367.374248798,
            "unit": "ns",
            "range": "± 4907.160365583359"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 698658.8253255208,
            "unit": "ns",
            "range": "± 2152.1759378770953"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4377.397486760066,
            "unit": "ns",
            "range": "± 7.641209228949684"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1879.3109367688496,
            "unit": "ns",
            "range": "± 1.185244651739981"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4385.61976975661,
            "unit": "ns",
            "range": "± 2.4037025018922304"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2100.790668487549,
            "unit": "ns",
            "range": "± 3.413142675144995"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1894.9098618825276,
            "unit": "ns",
            "range": "± 23.69508153640733"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3164.719108581543,
            "unit": "ns",
            "range": "± 7.6361778933777735"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2677.8545941670736,
            "unit": "ns",
            "range": "± 10.72239195905403"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 43789.518493652344,
            "unit": "ns",
            "range": "± 18.223416252850402"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 17364.95304283729,
            "unit": "ns",
            "range": "± 17.533493695651103"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18789.57125384991,
            "unit": "ns",
            "range": "± 10.479768330881914"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 37196.77401029147,
            "unit": "ns",
            "range": "± 66.40342835393767"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 34270.22223336356,
            "unit": "ns",
            "range": "± 295.37877988714445"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 440400.19208233175,
            "unit": "ns",
            "range": "± 370.95875154809323"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 189406.58654785156,
            "unit": "ns",
            "range": "± 81.99373085776013"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4415.947799137661,
            "unit": "ns",
            "range": "± 4.367915721385503"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73209.5662466196,
            "unit": "ns",
            "range": "± 176.38509737085144"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4416.142719562237,
            "unit": "ns",
            "range": "± 1.9969525915440656"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 297936.8074857272,
            "unit": "ns",
            "range": "± 207.90404740557832"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73406.5666422526,
            "unit": "ns",
            "range": "± 182.9086059241848"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3114.446264266968,
            "unit": "ns",
            "range": "± 5.764218655942408"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2694.502400324895,
            "unit": "ns",
            "range": "± 1.5642829865092986"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 44676.014236450195,
            "unit": "ns",
            "range": "± 27.664391570686696"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 30609146.239583332,
            "unit": "ns",
            "range": "± 12508.34456351117"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 710655.8544108073,
            "unit": "ns",
            "range": "± 451.637474883402"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 37080.71545879658,
            "unit": "ns",
            "range": "± 94.32830024299375"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 34034.63138020833,
            "unit": "ns",
            "range": "± 256.5553117217904"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 484663.4684244792,
            "unit": "ns",
            "range": "± 426.59321669992"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2004867996.7692308,
            "unit": "ns",
            "range": "± 1145627.8925534065"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4372.158484867641,
            "unit": "ns",
            "range": "± 3.2784397109377843"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 8973.760973612467,
            "unit": "ns",
            "range": "± 12.490754863342875"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 322388.88904747594,
            "unit": "ns",
            "range": "± 122.16478323896614"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3157.8463507432202,
            "unit": "ns",
            "range": "± 2.1141773130795034"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2589.749979400635,
            "unit": "ns",
            "range": "± 8.331171115176184"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44427.74258931478,
            "unit": "ns",
            "range": "± 21.946760450222413"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 18793.771924700057,
            "unit": "ns",
            "range": "± 9.81919620317832"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31338107.51785714,
            "unit": "ns",
            "range": "± 65358.69821183382"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 94728.64164381761,
            "unit": "ns",
            "range": "± 136.66353741926852"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 33223.62440708705,
            "unit": "ns",
            "range": "± 110.91220737904739"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 31141720.14,
            "unit": "ns",
            "range": "± 6516827.278957115"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 17796675.385,
            "unit": "ns",
            "range": "± 3972556.0827233884"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 14499130.69140625,
            "unit": "ns",
            "range": "± 2511994.6335657826"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 282924322.24137926,
            "unit": "ns",
            "range": "± 12220404.756479664"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 146679926.1328125,
            "unit": "ns",
            "range": "± 4451270.642887597"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 144524971.78214285,
            "unit": "ns",
            "range": "± 6985171.965426081"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 38027389.14125,
            "unit": "ns",
            "range": "± 4703982.956064404"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 22276591.37625,
            "unit": "ns",
            "range": "± 1520073.2789121282"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 293658590.5714286,
            "unit": "ns",
            "range": "± 5129528.424259886"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 159853371.69444445,
            "unit": "ns",
            "range": "± 3362681.9928330705"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1024)",
            "value": 98.92226458589236,
            "unit": "ns",
            "range": "± 0.0676171280624217"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1024)",
            "value": 682.9671375410898,
            "unit": "ns",
            "range": "± 1.3273083869369555"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1024)",
            "value": 96.75025762830462,
            "unit": "ns",
            "range": "± 0.04928590386203124"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1000000)",
            "value": 100300.88875638522,
            "unit": "ns",
            "range": "± 330.20231171345443"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1000000)",
            "value": 627652.0575474331,
            "unit": "ns",
            "range": "± 882.8791369278474"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1000000)",
            "value": 79661.61359863282,
            "unit": "ns",
            "range": "± 207.72889929584647"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7647.103286743164,
            "unit": "ns",
            "range": "± 2.871566037106633"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2558.943342062143,
            "unit": "ns",
            "range": "± 2.515756992659463"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7645.586313883464,
            "unit": "ns",
            "range": "± 1.0190631581251044"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2561.6458423321064,
            "unit": "ns",
            "range": "± 5.058656454390086"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8921.15595304049,
            "unit": "ns",
            "range": "± 3.0599160567733996"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 9466.063177490234,
            "unit": "ns",
            "range": "± 5.3159618078916"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8922.000961303711,
            "unit": "ns",
            "range": "± 1.7878877201921255"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 9453.736883980888,
            "unit": "ns",
            "range": "± 8.719909800384261"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 34968.90115152995,
            "unit": "ns",
            "range": "± 15.367065446013939"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 20347.96106160482,
            "unit": "ns",
            "range": "± 30.069448238166466"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 14171.880400962018,
            "unit": "ns",
            "range": "± 547.7471953886885"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 8199.399625651042,
            "unit": "ns",
            "range": "± 95.31349510263361"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 6388.213351909931,
            "unit": "ns",
            "range": "± 4.685624658574827"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 6390.43209177653,
            "unit": "ns",
            "range": "± 6.906558968774985"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 12901.819539896647,
            "unit": "ns",
            "range": "± 14.839768628827239"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 33601.76248873197,
            "unit": "ns",
            "range": "± 17.39880481371239"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 26197.491643269856,
            "unit": "ns",
            "range": "± 30.29269893624081"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 14995.190071105957,
            "unit": "ns",
            "range": "± 19.11372553417911"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 8949.05710543119,
            "unit": "ns",
            "range": "± 5.139689176532321"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5115.948302635779,
            "unit": "ns",
            "range": "± 2.8976474082577397"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5117.257966359456,
            "unit": "ns",
            "range": "± 2.6796581581888237"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 12155.365907033285,
            "unit": "ns",
            "range": "± 6.649520283835443"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 97291.37147623698,
            "unit": "ns",
            "range": "± 32.816636617183214"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 19950.304153442383,
            "unit": "ns",
            "range": "± 41.87645095587311"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 13421.477659959059,
            "unit": "ns",
            "range": "± 2.4785416443611523"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 9155.064284691443,
            "unit": "ns",
            "range": "± 3.848155073508629"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4247.539033253987,
            "unit": "ns",
            "range": "± 1.8328377370958313"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4100.155832730807,
            "unit": "ns",
            "range": "± 3.0766552759780983"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 11469.102043151855,
            "unit": "ns",
            "range": "± 1.887669632567007"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 1024)",
            "value": 1039.3634869893392,
            "unit": "ns",
            "range": "± 6.364211785167664"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 1024)",
            "value": 7.320084095001221,
            "unit": "ns",
            "range": "± 0.01009213198545238"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 65536)",
            "value": 172409.55693708148,
            "unit": "ns",
            "range": "± 164.26925210122604"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 65536)",
            "value": 327.86820599011014,
            "unit": "ns",
            "range": "± 0.26769522625360104"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Predictable(Length: 1000000)",
            "value": 627515.7643694197,
            "unit": "ns",
            "range": "± 496.1357022471851"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Predictable(Length: 1000000)",
            "value": 941759.8489583334,
            "unit": "ns",
            "range": "± 596.6074059059323"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1900083.6282087055,
            "unit": "ns",
            "range": "± 23577.011440107817"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 2924316.886458333,
            "unit": "ns",
            "range": "± 34036.5575633853"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2976482.60546875,
            "unit": "ns",
            "range": "± 21589.09864658898"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 1024)",
            "value": 1046.0882971627373,
            "unit": "ns",
            "range": "± 5.191158730063426"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 1024)",
            "value": 822.2873301506042,
            "unit": "ns",
            "range": "± 0.5441704406576541"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 65536)",
            "value": 172266.92890625,
            "unit": "ns",
            "range": "± 262.49008488610787"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 65536)",
            "value": 55064.27568641076,
            "unit": "ns",
            "range": "± 12.141381084947517"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 1024)",
            "value": 284.70054054260254,
            "unit": "ns",
            "range": "± 0.12818489804220182"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 1024)",
            "value": 244.95356397628785,
            "unit": "ns",
            "range": "± 0.2451998117747584"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 65536)",
            "value": 15768.320796421596,
            "unit": "ns",
            "range": "± 13.318387753741755"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 65536)",
            "value": 15461.479571024576,
            "unit": "ns",
            "range": "± 39.189692023508705"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 14183490.563466495,
            "unit": "ns",
            "range": "± 2987901.1866532494"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3301684.034598214,
            "unit": "ns",
            "range": "± 9805.791739118742"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Unpredictable(Length: 1000000)",
            "value": 4622837.611979167,
            "unit": "ns",
            "range": "± 1500.3369553042069"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Unpredictable(Length: 1000000)",
            "value": 945726.0099158654,
            "unit": "ns",
            "range": "± 4145.922599127991"
          },
          {
            "name": "GuidBenchmark.V4_BclNewGuid",
            "value": 2438062.269791667,
            "unit": "ns",
            "range": "± 2532.705896799754"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_Xoshiro",
            "value": 73532.85201322116,
            "unit": "ns",
            "range": "± 44.182285863162846"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_WyRand",
            "value": 72739.27212960379,
            "unit": "ns",
            "range": "± 30.30782598522167"
          },
          {
            "name": "GuidBenchmark.V7_BclNewGuid",
            "value": 2443361.2176339286,
            "unit": "ns",
            "range": "± 2654.667947825812"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Stateless",
            "value": 68580.66493443081,
            "unit": "ns",
            "range": "± 94.71370495832468"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Monotonic",
            "value": 62527.8268737793,
            "unit": "ns",
            "range": "± 30.911320666571843"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13702829.636363637,
            "unit": "ns",
            "range": "± 330700.4758863835"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5877265.4375,
            "unit": "ns",
            "range": "± 231980.9940477889"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5904900.324324325,
            "unit": "ns",
            "range": "± 138479.23536863126"
          }
        ]
      },
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
          "id": "60a62bf446f981469a0511c1ab8e4b28f50c5bfd",
          "message": "Merge pull request #234 from marius-bughiu/fix/issue-233-indexer-overwrite-version",
          "timestamp": "2026-06-26T06:16:27Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/60a62bf446f981469a0511c1ab8e4b28f50c5bfd"
        },
        "date": 1782723852430,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1024)",
            "value": 327.7027813707079,
            "unit": "ns",
            "range": "± 0.5356629013205393"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1024)",
            "value": 32.164366223982405,
            "unit": "ns",
            "range": "± 0.1489406036482423"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1024)",
            "value": 141.7891563635606,
            "unit": "ns",
            "range": "± 0.05851941986674987"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1000000)",
            "value": 313500.2513671875,
            "unit": "ns",
            "range": "± 263.5236783956998"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1000000)",
            "value": 50392.490486653645,
            "unit": "ns",
            "range": "± 118.78964501240439"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1000000)",
            "value": 139632.96358548678,
            "unit": "ns",
            "range": "± 86.6244099710584"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1700631.8680989584,
            "unit": "ns",
            "range": "± 9874.081258113652"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 759076.1255859375,
            "unit": "ns",
            "range": "± 12735.40123461274"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1818195.6924479166,
            "unit": "ns",
            "range": "± 8589.19835320841"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2441092.7705729166,
            "unit": "ns",
            "range": "± 17014.08347799254"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1142282.7777622768,
            "unit": "ns",
            "range": "± 18031.62076436483"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2606566.2841145834,
            "unit": "ns",
            "range": "± 29226.336931788628"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4826852.049665178,
            "unit": "ns",
            "range": "± 25266.105109137658"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2314788.53125,
            "unit": "ns",
            "range": "± 29184.05612249949"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 5206349.546354166,
            "unit": "ns",
            "range": "± 73955.07676610287"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 73984.42096819196,
            "unit": "ns",
            "range": "± 82.96640922826549"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 27059.883623758953,
            "unit": "ns",
            "range": "± 56.32438023503852"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 13038.093003409249,
            "unit": "ns",
            "range": "± 85.46375095245457"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 7170.926207987467,
            "unit": "ns",
            "range": "± 36.66277825826606"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3574122.0634765625,
            "unit": "ns",
            "range": "± 6366.224200327391"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1816345.0026692708,
            "unit": "ns",
            "range": "± 27651.460698953477"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_NaiveLoop",
            "value": 29968.614160391,
            "unit": "ns",
            "range": "± 26.137702970919293"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_MathLog10",
            "value": 42146.05380045573,
            "unit": "ns",
            "range": "± 16.298028329163223"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_FastUtils",
            "value": 3347.970522880554,
            "unit": "ns",
            "range": "± 1.4895655109192867"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_NaiveLoop",
            "value": 99323.26276573769,
            "unit": "ns",
            "range": "± 30.5265955882512"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_MathLog10",
            "value": 42020.79397583008,
            "unit": "ns",
            "range": "± 46.229339530284136"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_FastUtils",
            "value": 8386.35250200544,
            "unit": "ns",
            "range": "± 10.299933763745678"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7642.554152352469,
            "unit": "ns",
            "range": "± 1.7612688109016252"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2983.482781336858,
            "unit": "ns",
            "range": "± 0.9421074850705344"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7643.355910164969,
            "unit": "ns",
            "range": "± 2.5163406045889563"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2984.3185600867637,
            "unit": "ns",
            "range": "± 1.500641252301433"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8916.123168945312,
            "unit": "ns",
            "range": "± 1.002517123711011"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 6423.213739248423,
            "unit": "ns",
            "range": "± 1.8184974913016934"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8915.86655484713,
            "unit": "ns",
            "range": "± 0.627561693641482"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 6424.99895125169,
            "unit": "ns",
            "range": "± 1.9729936312761334"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 86026.27057354267,
            "unit": "ns",
            "range": "± 43.12728199557974"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 22195.418572998045,
            "unit": "ns",
            "range": "± 34.803701045172645"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 942957.49765625,
            "unit": "ns",
            "range": "± 78538.70193335967"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 827682.3113511029,
            "unit": "ns",
            "range": "± 16498.622036853205"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 824818.1864536831,
            "unit": "ns",
            "range": "± 10971.625082537641"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 4555931.946328125,
            "unit": "ns",
            "range": "± 786043.9040177648"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4926301.984375,
            "unit": "ns",
            "range": "± 134159.81691049863"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 4834688.6734375,
            "unit": "ns",
            "range": "± 51112.1406761226"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4400825.916466346,
            "unit": "ns",
            "range": "± 1880.591795552473"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1885744.8182842548,
            "unit": "ns",
            "range": "± 752.2877775500172"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13775.987171718052,
            "unit": "ns",
            "range": "± 208.57871773622284"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6745.919093540737,
            "unit": "ns",
            "range": "± 35.70875835700628"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 11802.87524210612,
            "unit": "ns",
            "range": "± 56.63428694344921"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6728.21992910476,
            "unit": "ns",
            "range": "± 241.29607613840045"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 5874.992679380021,
            "unit": "ns",
            "range": "± 233.01399008133006"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7247.285224914551,
            "unit": "ns",
            "range": "± 37.54911294088281"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7696.25683430263,
            "unit": "ns",
            "range": "± 220.44705262523132"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 157359.84783528646,
            "unit": "ns",
            "range": "± 1035.683480840646"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 37831.17776489258,
            "unit": "ns",
            "range": "± 222.1923901666483"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 37761.703975423174,
            "unit": "ns",
            "range": "± 224.22845518521464"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 44165.176321847095,
            "unit": "ns",
            "range": "± 256.33796546190246"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 41896.29854125976,
            "unit": "ns",
            "range": "± 160.3967939668054"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 3944024.6381835938,
            "unit": "ns",
            "range": "± 75958.65357445758"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4707763.545833333,
            "unit": "ns",
            "range": "± 36865.81265629732"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 12458.160675048828,
            "unit": "ns",
            "range": "± 76.87208302858659"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6593.06676534017,
            "unit": "ns",
            "range": "± 48.18015720627804"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 8787.715615844727,
            "unit": "ns",
            "range": "± 53.50654543154719"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4731.900973510742,
            "unit": "ns",
            "range": "± 125.79024712214542"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 5161.600484287038,
            "unit": "ns",
            "range": "± 164.2882100884273"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7112.327858988444,
            "unit": "ns",
            "range": "± 39.7954802216027"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6670.948863689716,
            "unit": "ns",
            "range": "± 75.94134611487861"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 136966.50704251803,
            "unit": "ns",
            "range": "± 293.72525664916975"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 28736.19321899414,
            "unit": "ns",
            "range": "± 210.34656488668932"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 30004.842255655924,
            "unit": "ns",
            "range": "± 184.51106156907628"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 44028.080082820015,
            "unit": "ns",
            "range": "± 105.59052587873816"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 42071.71972220285,
            "unit": "ns",
            "range": "± 121.3586101739509"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3012303.41953125,
            "unit": "ns",
            "range": "± 223942.7245810441"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2000886.162109375,
            "unit": "ns",
            "range": "± 45728.95077881067"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 12928.515461848332,
            "unit": "ns",
            "range": "± 42.60956306127192"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6631.376206970215,
            "unit": "ns",
            "range": "± 36.065784825128596"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 228209.76118977866,
            "unit": "ns",
            "range": "± 89.88739475328991"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 474309.1089274089,
            "unit": "ns",
            "range": "± 141.07013355697478"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 165029.20349121094,
            "unit": "ns",
            "range": "± 105.28654576816058"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6998.7315144856775,
            "unit": "ns",
            "range": "± 40.152079145687296"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6656.088924662272,
            "unit": "ns",
            "range": "± 117.84120751920413"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 137473.01166992186,
            "unit": "ns",
            "range": "± 605.5016451557553"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 30592296.745833334,
            "unit": "ns",
            "range": "± 14837.21901535579"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 719993.2143554688,
            "unit": "ns",
            "range": "± 599.3260374972244"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 44183.18375338041,
            "unit": "ns",
            "range": "± 295.9652949451899"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 42093.68191935222,
            "unit": "ns",
            "range": "± 259.15382271345146"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3386031.75390625,
            "unit": "ns",
            "range": "± 249361.06902838763"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 4601917461.769231,
            "unit": "ns",
            "range": "± 1510150.6178768591"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6760.833986554827,
            "unit": "ns",
            "range": "± 76.71369777527102"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4846.611729939778,
            "unit": "ns",
            "range": "± 99.80280266782542"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 515606.8623046875,
            "unit": "ns",
            "range": "± 149.50232759362737"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7521.898100789388,
            "unit": "ns",
            "range": "± 65.14147944038731"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6324.966623033796,
            "unit": "ns",
            "range": "± 82.52577410480349"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 139147.56137695312,
            "unit": "ns",
            "range": "± 661.2760056383352"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 28647.610063680015,
            "unit": "ns",
            "range": "± 135.50886568568862"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31266715.855769232,
            "unit": "ns",
            "range": "± 15355.837278242936"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44819.93065534319,
            "unit": "ns",
            "range": "± 507.08922919842075"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 42068.56549508231,
            "unit": "ns",
            "range": "± 144.32490126477228"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 19512430.49776786,
            "unit": "ns",
            "range": "± 174526.76451748662"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 22880815.45625,
            "unit": "ns",
            "range": "± 112081.67545410927"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 22963325.19419643,
            "unit": "ns",
            "range": "± 121357.29376602719"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 228500693.71317837,
            "unit": "ns",
            "range": "± 8436314.268092884"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 135133191.34188035,
            "unit": "ns",
            "range": "± 4693457.9618457"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 135219709.65789473,
            "unit": "ns",
            "range": "± 2974916.309448888"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 22112203.703125,
            "unit": "ns",
            "range": "± 379043.20078003866"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 28997611.423958335,
            "unit": "ns",
            "range": "± 295939.0631831633"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 247867551.7222222,
            "unit": "ns",
            "range": "± 5201448.088126269"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 146426678.28125,
            "unit": "ns",
            "range": "± 3164963.1418550373"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 11184.000712468074,
            "unit": "ns",
            "range": "± 33.11771145889081"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 3283.3437459309894,
            "unit": "ns",
            "range": "± 17.977136758394447"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3432871.6611979166,
            "unit": "ns",
            "range": "± 29746.40019090251"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1263374.6793428308,
            "unit": "ns",
            "range": "± 23104.200500059327"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4364.7676016000605,
            "unit": "ns",
            "range": "± 1.0561110141767656"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4788.589562552316,
            "unit": "ns",
            "range": "± 8.46380017797646"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 322288.5787876674,
            "unit": "ns",
            "range": "± 154.62945779899076"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 2980.191999582144,
            "unit": "ns",
            "range": "± 1.718801484357673"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2654.015284983317,
            "unit": "ns",
            "range": "± 5.906117551784816"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2168.9361158098495,
            "unit": "ns",
            "range": "± 4.092012190597354"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2496.4341634114585,
            "unit": "ns",
            "range": "± 17.057730136992934"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 44309.3151174692,
            "unit": "ns",
            "range": "± 24.56055408322888"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 31262839.0875,
            "unit": "ns",
            "range": "± 14947.079284202535"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 33368.36479773888,
            "unit": "ns",
            "range": "± 98.78320264951054"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1622926.1119791667,
            "unit": "ns",
            "range": "± 599.324571758647"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 955231.0659555289,
            "unit": "ns",
            "range": "± 1457.3304886452217"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 716828.7946614583,
            "unit": "ns",
            "range": "± 2194.082332983303"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 705760.6027994792,
            "unit": "ns",
            "range": "± 1434.9045402709135"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4756.1894525800435,
            "unit": "ns",
            "range": "± 8.950322457864587"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2170.9792427649863,
            "unit": "ns",
            "range": "± 2.706602926484501"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4772.162743631999,
            "unit": "ns",
            "range": "± 9.759924187931347"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2017.6071220397948,
            "unit": "ns",
            "range": "± 5.871922182360841"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2299.0606236775716,
            "unit": "ns",
            "range": "± 9.17255558862543"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3204.4561854771205,
            "unit": "ns",
            "range": "± 7.860854393711353"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2763.067844390869,
            "unit": "ns",
            "range": "± 2.6671869775767267"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 84914.86479577352,
            "unit": "ns",
            "range": "± 3128.460574420421"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 94482.08653846153,
            "unit": "ns",
            "range": "± 25.100531187335253"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 26348.807052612305,
            "unit": "ns",
            "range": "± 73.99315628364108"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 35880.31806291853,
            "unit": "ns",
            "range": "± 44.80278595875703"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 33789.351279122486,
            "unit": "ns",
            "range": "± 87.88737859732753"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1608048.0658482143,
            "unit": "ns",
            "range": "± 2922.823765611934"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 687059.9305989583,
            "unit": "ns",
            "range": "± 1508.4789424571265"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4369.5020100520205,
            "unit": "ns",
            "range": "± 4.377831612454716"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1911.5737920907827,
            "unit": "ns",
            "range": "± 1.3182120298706501"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4373.88216603597,
            "unit": "ns",
            "range": "± 6.106131520476668"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1876.2437377929687,
            "unit": "ns",
            "range": "± 1.7156183808503274"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2047.8403275807698,
            "unit": "ns",
            "range": "± 3.189346380114322"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3164.2299646230845,
            "unit": "ns",
            "range": "± 4.725158033911635"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2672.1101198832193,
            "unit": "ns",
            "range": "± 11.678248816784565"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 43779.83432006836,
            "unit": "ns",
            "range": "± 94.97917215257307"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18011.653638567244,
            "unit": "ns",
            "range": "± 11.170025237303724"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18801.84795320951,
            "unit": "ns",
            "range": "± 11.579547771825101"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 37345.53290230887,
            "unit": "ns",
            "range": "± 76.29042202102188"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 34036.84149169922,
            "unit": "ns",
            "range": "± 188.89445494181842"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 439229.0560021034,
            "unit": "ns",
            "range": "± 187.6055956409655"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 189085.8978553185,
            "unit": "ns",
            "range": "± 186.03210577966163"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4409.72912480281,
            "unit": "ns",
            "range": "± 1.958603509315981"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73494.04415690104,
            "unit": "ns",
            "range": "± 398.35935078005815"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4413.880147493803,
            "unit": "ns",
            "range": "± 2.274731842910527"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 297820.6380333534,
            "unit": "ns",
            "range": "± 169.63879828639247"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73337.89921351841,
            "unit": "ns",
            "range": "± 202.9668243644101"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3107.116620577299,
            "unit": "ns",
            "range": "± 3.904994253528432"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2696.1928713662282,
            "unit": "ns",
            "range": "± 1.0872641530826943"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 44684.05221792368,
            "unit": "ns",
            "range": "± 18.55502248951219"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 30606009.764423076,
            "unit": "ns",
            "range": "± 19046.923234320828"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 710108.9039481027,
            "unit": "ns",
            "range": "± 339.45789610718714"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 37251.967673165454,
            "unit": "ns",
            "range": "± 89.2579250749669"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 34027.19406563895,
            "unit": "ns",
            "range": "± 155.91276209174833"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 484439.3359723772,
            "unit": "ns",
            "range": "± 281.6397216642036"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2006047258.5,
            "unit": "ns",
            "range": "± 2786223.7431138214"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4592.0571875939,
            "unit": "ns",
            "range": "± 12.044507202891868"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 1877.8767880031041,
            "unit": "ns",
            "range": "± 0.9531532830823167"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 324557.03662109375,
            "unit": "ns",
            "range": "± 149.42741012918665"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3181.2406697954452,
            "unit": "ns",
            "range": "± 1.5177106151283788"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2588.8023106711253,
            "unit": "ns",
            "range": "± 8.220269103940696"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44660.66192626953,
            "unit": "ns",
            "range": "± 27.263336803920943"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 16929.010482788086,
            "unit": "ns",
            "range": "± 14.839243308784114"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31274617.158653848,
            "unit": "ns",
            "range": "± 13962.68780931618"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 37152.00704956055,
            "unit": "ns",
            "range": "± 145.04037359537836"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 33886.088970477766,
            "unit": "ns",
            "range": "± 299.69143858527826"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 17164714.353515625,
            "unit": "ns",
            "range": "± 310496.69184560183"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 8364892.53125,
            "unit": "ns",
            "range": "± 37167.87565827567"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 8519588.332589285,
            "unit": "ns",
            "range": "± 55197.26980908436"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 216702501.88235295,
            "unit": "ns",
            "range": "± 4273052.84216861"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 125448575.71153846,
            "unit": "ns",
            "range": "± 701905.6453348007"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 122581071.57333332,
            "unit": "ns",
            "range": "± 1975574.4917291964"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 20144009.02581522,
            "unit": "ns",
            "range": "± 773116.7491732175"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 15655065.54296875,
            "unit": "ns",
            "range": "± 395551.7621110585"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 223776258.93939397,
            "unit": "ns",
            "range": "± 5375503.509643898"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 142112886.98333332,
            "unit": "ns",
            "range": "± 1139557.950589196"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1024)",
            "value": 98.81502991063255,
            "unit": "ns",
            "range": "± 0.14959037703925948"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1024)",
            "value": 675.3320734160287,
            "unit": "ns",
            "range": "± 0.4341560912240601"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1024)",
            "value": 96.72298866051894,
            "unit": "ns",
            "range": "± 0.157297451687047"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1000000)",
            "value": 102036.44866071429,
            "unit": "ns",
            "range": "± 114.83785333426304"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1000000)",
            "value": 625904.5868443081,
            "unit": "ns",
            "range": "± 431.55612670270614"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1000000)",
            "value": 79847.3224609375,
            "unit": "ns",
            "range": "± 102.1067465531821"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7641.0482228597,
            "unit": "ns",
            "range": "± 1.1368964747985628"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2559.5240041097004,
            "unit": "ns",
            "range": "± 3.447622159503048"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7642.4418694632395,
            "unit": "ns",
            "range": "± 2.4211921112320054"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2557.8508020128525,
            "unit": "ns",
            "range": "± 0.9815044470366652"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8917.951303100586,
            "unit": "ns",
            "range": "± 3.0681395935827727"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 9441.865469712477,
            "unit": "ns",
            "range": "± 2.226447682202341"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8915.780497624324,
            "unit": "ns",
            "range": "± 1.293375726464893"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 9456.420247395834,
            "unit": "ns",
            "range": "± 11.073849556837027"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 34895.71395002092,
            "unit": "ns",
            "range": "± 15.620966010843086"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 20327.13169148763,
            "unit": "ns",
            "range": "± 57.09613211028287"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 13960.506247300367,
            "unit": "ns",
            "range": "± 379.2325586151623"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 8167.022997538249,
            "unit": "ns",
            "range": "± 83.27516135068161"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 6383.808102925618,
            "unit": "ns",
            "range": "± 2.7508053611328354"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 6386.570089975993,
            "unit": "ns",
            "range": "± 2.577968887022151"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 12889.01791636149,
            "unit": "ns",
            "range": "± 11.534290031915141"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 33620.986227852954,
            "unit": "ns",
            "range": "± 13.97476962025104"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 26291.54285975865,
            "unit": "ns",
            "range": "± 47.24059811232787"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 14993.572901044574,
            "unit": "ns",
            "range": "± 1.8630208376761586"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 8942.793772770809,
            "unit": "ns",
            "range": "± 7.044209409310461"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5117.122207641602,
            "unit": "ns",
            "range": "± 1.5427238386698516"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5115.921803065708,
            "unit": "ns",
            "range": "± 3.2038575435959626"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 12150.953639103816,
            "unit": "ns",
            "range": "± 7.779044239869205"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 97238.22651163737,
            "unit": "ns",
            "range": "± 24.401654696454457"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 19957.710001627605,
            "unit": "ns",
            "range": "± 42.4078113454248"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 13418.445590427944,
            "unit": "ns",
            "range": "± 2.8217631221281354"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 9149.641177837666,
            "unit": "ns",
            "range": "± 2.1723909509293993"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4245.939453125,
            "unit": "ns",
            "range": "± 1.5449362167395522"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4097.601254599435,
            "unit": "ns",
            "range": "± 2.675572196836061"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 11468.360392056979,
            "unit": "ns",
            "range": "± 5.503141217384699"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 1024)",
            "value": 1054.6997049967447,
            "unit": "ns",
            "range": "± 7.256156995465725"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 1024)",
            "value": 7.167168662945429,
            "unit": "ns",
            "range": "± 0.004968693347108556"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 65536)",
            "value": 172579.1624186198,
            "unit": "ns",
            "range": "± 365.55396383685644"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 65536)",
            "value": 327.8310761451721,
            "unit": "ns",
            "range": "± 0.19802871241380418"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Predictable(Length: 1000000)",
            "value": 626230.6709681919,
            "unit": "ns",
            "range": "± 282.4782089121731"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Predictable(Length: 1000000)",
            "value": 942771.7702473958,
            "unit": "ns",
            "range": "± 1127.3184960694157"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1845588.6296223958,
            "unit": "ns",
            "range": "± 27168.122828728432"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 2894223.937717014,
            "unit": "ns",
            "range": "± 59354.18636335022"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2876628.2567708334,
            "unit": "ns",
            "range": "± 43790.303556770195"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 1024)",
            "value": 1037.0039512634278,
            "unit": "ns",
            "range": "± 11.377548883159054"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 1024)",
            "value": 822.2534284591675,
            "unit": "ns",
            "range": "± 0.704965675190526"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 65536)",
            "value": 172286.0000174386,
            "unit": "ns",
            "range": "± 225.4130157630736"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 65536)",
            "value": 55069.102150472005,
            "unit": "ns",
            "range": "± 19.676871664166203"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 1024)",
            "value": 284.5233163152422,
            "unit": "ns",
            "range": "± 0.15906121376351728"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 1024)",
            "value": 244.9052371184031,
            "unit": "ns",
            "range": "± 0.13933605731917464"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 65536)",
            "value": 15975.794036865234,
            "unit": "ns",
            "range": "± 10.187182694287717"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 65536)",
            "value": 15330.44700739934,
            "unit": "ns",
            "range": "± 11.744467930592089"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 10922901.317950582,
            "unit": "ns",
            "range": "± 374384.31943503354"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3304380.0885416665,
            "unit": "ns",
            "range": "± 5566.330077774297"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Unpredictable(Length: 1000000)",
            "value": 4621356.168870192,
            "unit": "ns",
            "range": "± 1459.0155155211469"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Unpredictable(Length: 1000000)",
            "value": 941135.8413783482,
            "unit": "ns",
            "range": "± 716.5792963715986"
          },
          {
            "name": "GuidBenchmark.V4_BclNewGuid",
            "value": 2449411.61328125,
            "unit": "ns",
            "range": "± 8158.576843992612"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_Xoshiro",
            "value": 73665.27664620536,
            "unit": "ns",
            "range": "± 57.352831614337646"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_WyRand",
            "value": 72450.7450796274,
            "unit": "ns",
            "range": "± 52.69933659740108"
          },
          {
            "name": "GuidBenchmark.V7_BclNewGuid",
            "value": 2445123.0452008927,
            "unit": "ns",
            "range": "± 2573.8334826307096"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Stateless",
            "value": 68495.21153157552,
            "unit": "ns",
            "range": "± 60.13003144668595"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Monotonic",
            "value": 62500.49984305246,
            "unit": "ns",
            "range": "± 29.322396480554666"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13856443.423076924,
            "unit": "ns",
            "range": "± 370447.5033902832"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5805841.521739131,
            "unit": "ns",
            "range": "± 127459.02263843059"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5791028.297297297,
            "unit": "ns",
            "range": "± 150097.94035800328"
          }
        ]
      },
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
          "id": "0e85cd344e2077221cb27fbb7467c015ab8694be",
          "message": "Merge pull request #243 from marius-bughiu/feat/robin-hood-set\n\nfeat(collections): add RobinHoodSet<T, THasher> — Robin Hood open-addressed set",
          "timestamp": "2026-07-06T05:34:24Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/0e85cd344e2077221cb27fbb7467c015ab8694be"
        },
        "date": 1783328836776,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1024)",
            "value": 327.8452265262604,
            "unit": "ns",
            "range": "± 0.6077473408800744"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1024)",
            "value": 31.992033596222218,
            "unit": "ns",
            "range": "± 0.04184906806533837"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1024)",
            "value": 141.99084361961908,
            "unit": "ns",
            "range": "± 0.2201396117883298"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1000000)",
            "value": 314181.00153996394,
            "unit": "ns",
            "range": "± 222.67858706874674"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1000000)",
            "value": 67938.29853166852,
            "unit": "ns",
            "range": "± 825.2322403232024"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1000000)",
            "value": 139937.40330153244,
            "unit": "ns",
            "range": "± 108.19668875810017"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1709457.165736607,
            "unit": "ns",
            "range": "± 8180.477200906316"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 747215.5779296875,
            "unit": "ns",
            "range": "± 9878.277308532015"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1829582.5306919643,
            "unit": "ns",
            "range": "± 10462.774576575343"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2408604.606971154,
            "unit": "ns",
            "range": "± 11670.472060988955"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1168457.5776692708,
            "unit": "ns",
            "range": "± 9808.577650664889"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2547070.568917411,
            "unit": "ns",
            "range": "± 14442.267247345793"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4934720.425223215,
            "unit": "ns",
            "range": "± 57488.03398849567"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2304463.0915178573,
            "unit": "ns",
            "range": "± 30645.799142693748"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4893964.074573863,
            "unit": "ns",
            "range": "± 137650.54998277174"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 79458.96166992188,
            "unit": "ns",
            "range": "± 41.81889459941147"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 27724.246420118543,
            "unit": "ns",
            "range": "± 921.7104467756582"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 13298.727365112305,
            "unit": "ns",
            "range": "± 225.5197308892966"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 6823.549508666993,
            "unit": "ns",
            "range": "± 101.94909214361093"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3681071.240104167,
            "unit": "ns",
            "range": "± 52552.007319272874"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1883883.4231770833,
            "unit": "ns",
            "range": "± 31722.772070791263"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_NaiveLoop",
            "value": 29976.546295166016,
            "unit": "ns",
            "range": "± 25.526094874564553"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_MathLog10",
            "value": 42170.01292637416,
            "unit": "ns",
            "range": "± 30.403384958350987"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_FastUtils",
            "value": 3346.258038154015,
            "unit": "ns",
            "range": "± 1.3191880713057083"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_NaiveLoop",
            "value": 99375.50936889648,
            "unit": "ns",
            "range": "± 34.541883570079534"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_MathLog10",
            "value": 42236.13255896935,
            "unit": "ns",
            "range": "± 24.106770794380463"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_FastUtils",
            "value": 8389.542415618896,
            "unit": "ns",
            "range": "± 5.293713827100518"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7645.215485890706,
            "unit": "ns",
            "range": "± 6.89934607033242"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2974.719675064087,
            "unit": "ns",
            "range": "± 3.0130829761552484"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7648.820619855608,
            "unit": "ns",
            "range": "± 6.732952570304695"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2980.9352428729717,
            "unit": "ns",
            "range": "± 5.430839058743235"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8933.419682094029,
            "unit": "ns",
            "range": "± 23.21653363552101"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 6416.845380147298,
            "unit": "ns",
            "range": "± 1.5754724895628736"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8914.759451059195,
            "unit": "ns",
            "range": "± 2.0325207885430374"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 6432.826188894419,
            "unit": "ns",
            "range": "± 11.44832493542611"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 86922.3434273856,
            "unit": "ns",
            "range": "± 417.14652048410477"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 22240.019300188338,
            "unit": "ns",
            "range": "± 48.007978756628766"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 941765.7636035156,
            "unit": "ns",
            "range": "± 71786.05741795918"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 835475.9001302083,
            "unit": "ns",
            "range": "± 11150.133962022777"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 830333.653125,
            "unit": "ns",
            "range": "± 9506.611328385612"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 4127571.3175,
            "unit": "ns",
            "range": "± 789527.8562835639"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4993066.640625,
            "unit": "ns",
            "range": "± 77541.91694866739"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 4902606.025520833,
            "unit": "ns",
            "range": "± 89802.69084722495"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4405899.733723958,
            "unit": "ns",
            "range": "± 5886.094963366976"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 2156184.679427083,
            "unit": "ns",
            "range": "± 18282.909672205984"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13868.25181082792,
            "unit": "ns",
            "range": "± 507.50756452644237"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6650.704493204753,
            "unit": "ns",
            "range": "± 22.446344367477685"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 12440.018624369304,
            "unit": "ns",
            "range": "± 226.77585540030282"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 5713.219478352865,
            "unit": "ns",
            "range": "± 52.3356424561906"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6483.674669333065,
            "unit": "ns",
            "range": "± 348.7774409279006"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7570.992275238037,
            "unit": "ns",
            "range": "± 132.10551357707067"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7199.081105470657,
            "unit": "ns",
            "range": "± 218.36260693509126"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 159190.73592703682,
            "unit": "ns",
            "range": "± 672.2479909137421"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 39921.65350545247,
            "unit": "ns",
            "range": "± 295.97783139671685"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 39200.108939615886,
            "unit": "ns",
            "range": "± 274.6653205466247"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 44907.18504987444,
            "unit": "ns",
            "range": "± 211.74748577191215"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 42414.17683105469,
            "unit": "ns",
            "range": "± 401.09899040529916"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4135905.521599265,
            "unit": "ns",
            "range": "± 79285.80396006208"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4863304.275520833,
            "unit": "ns",
            "range": "± 46380.335369234635"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 14235.101104418436,
            "unit": "ns",
            "range": "± 549.9634666527758"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6817.626086895282,
            "unit": "ns",
            "range": "± 58.398992339371226"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 9934.313036072928,
            "unit": "ns",
            "range": "± 411.1052226992369"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4813.812634362115,
            "unit": "ns",
            "range": "± 102.82521174603914"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 5021.456894429525,
            "unit": "ns",
            "range": "± 64.82652274736085"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7467.707284037272,
            "unit": "ns",
            "range": "± 68.65975914730281"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7397.658949534099,
            "unit": "ns",
            "range": "± 150.86881367704578"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 139275.64624023438,
            "unit": "ns",
            "range": "± 638.1385113923324"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 30228.88603515625,
            "unit": "ns",
            "range": "± 196.06496816490602"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 29988.625397745767,
            "unit": "ns",
            "range": "± 553.6978182714219"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 46172.79895019531,
            "unit": "ns",
            "range": "± 547.9902854062057"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 44037.99161202567,
            "unit": "ns",
            "range": "± 551.2303051342044"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3467388.971015625,
            "unit": "ns",
            "range": "± 380932.4043004264"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2047551.4227764423,
            "unit": "ns",
            "range": "± 54082.351903661554"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 13586.866141686072,
            "unit": "ns",
            "range": "± 194.36377375372223"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6927.010063171387,
            "unit": "ns",
            "range": "± 108.47235205085407"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 228369.17087965744,
            "unit": "ns",
            "range": "± 475.53746426610417"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 475469.4904972957,
            "unit": "ns",
            "range": "± 603.4701665225433"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 165412.90360201322,
            "unit": "ns",
            "range": "± 125.27355765913342"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7416.706106185913,
            "unit": "ns",
            "range": "± 186.28076509055123"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7197.036516781511,
            "unit": "ns",
            "range": "± 315.1329324326978"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 141221.63050130208,
            "unit": "ns",
            "range": "± 770.1936142683078"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 30607057.324519232,
            "unit": "ns",
            "range": "± 13925.169716730064"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 722152.5211588541,
            "unit": "ns",
            "range": "± 619.4528226529073"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 46571.21457316081,
            "unit": "ns",
            "range": "± 710.928248600368"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 42610.77776896159,
            "unit": "ns",
            "range": "± 453.2774339240372"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3506545.84078125,
            "unit": "ns",
            "range": "± 260407.89341823306"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 4606309698.466666,
            "unit": "ns",
            "range": "± 4916031.590452781"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6895.165088653564,
            "unit": "ns",
            "range": "± 131.65391457101688"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4451.751591212313,
            "unit": "ns",
            "range": "± 185.7238794072606"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 516219.80528846156,
            "unit": "ns",
            "range": "± 426.89387702227515"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7865.119451250349,
            "unit": "ns",
            "range": "± 113.16628381858118"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7256.942677021027,
            "unit": "ns",
            "range": "± 139.19244043933202"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 140583.00398763022,
            "unit": "ns",
            "range": "± 875.6341180477316"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 29973.820241292316,
            "unit": "ns",
            "range": "± 867.9819655158105"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31271389.533653848,
            "unit": "ns",
            "range": "± 8480.501648220981"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 46108.666579026445,
            "unit": "ns",
            "range": "± 382.66221423379943"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 43094.131990559894,
            "unit": "ns",
            "range": "± 732.350947374684"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 19945148.249503966,
            "unit": "ns",
            "range": "± 916004.1858553765"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 23222084.86830357,
            "unit": "ns",
            "range": "± 214147.22628018423"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 23199385.1125,
            "unit": "ns",
            "range": "± 327914.8660576775"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 240429531.69444445,
            "unit": "ns",
            "range": "± 13300305.551156152"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 141089934.83333334,
            "unit": "ns",
            "range": "± 2491928.477275583"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 143480375.7159091,
            "unit": "ns",
            "range": "± 3430382.6158048166"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 23501474.325657893,
            "unit": "ns",
            "range": "± 518797.52114894585"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 29669402.1125,
            "unit": "ns",
            "range": "± 331257.57765051984"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 260654913.29268292,
            "unit": "ns",
            "range": "± 9219257.067982113"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 156101091.64646468,
            "unit": "ns",
            "range": "± 4707173.949256934"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 11244.926380411784,
            "unit": "ns",
            "range": "± 51.07520491845444"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 3294.6111485617503,
            "unit": "ns",
            "range": "± 38.01180698381025"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3415624.0109375,
            "unit": "ns",
            "range": "± 39741.495455878"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1313915.8705915178,
            "unit": "ns",
            "range": "± 43085.539825215135"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4533.98745564052,
            "unit": "ns",
            "range": "± 5.797702395381609"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4803.358008248465,
            "unit": "ns",
            "range": "± 17.154057089191284"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 322547.33024088544,
            "unit": "ns",
            "range": "± 129.25081284596484"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 2985.1046289716446,
            "unit": "ns",
            "range": "± 3.864191492409071"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2776.1420062138486,
            "unit": "ns",
            "range": "± 3.333976212858764"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2171.870748247419,
            "unit": "ns",
            "range": "± 6.063085379996304"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2491.1282002585276,
            "unit": "ns",
            "range": "± 15.939612721853063"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 44440.418395996094,
            "unit": "ns",
            "range": "± 27.02102072163473"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 31271611.769230768,
            "unit": "ns",
            "range": "± 12004.755073196531"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 32397.58352426382,
            "unit": "ns",
            "range": "± 73.74157187248112"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1636458.7068810095,
            "unit": "ns",
            "range": "± 7922.4627179630725"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 974688.5696149553,
            "unit": "ns",
            "range": "± 1949.0986226464079"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 720230.2082868303,
            "unit": "ns",
            "range": "± 2312.9537271464783"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 710290.2920572917,
            "unit": "ns",
            "range": "± 3203.125134820628"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4795.442262502817,
            "unit": "ns",
            "range": "± 5.671626216682584"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2195.6713160001314,
            "unit": "ns",
            "range": "± 4.44062257799978"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4754.224068232945,
            "unit": "ns",
            "range": "± 28.198544507003685"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 1998.7915172576904,
            "unit": "ns",
            "range": "± 5.7830479258879475"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2291.8513482900767,
            "unit": "ns",
            "range": "± 5.248858756160282"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3140.8191472567046,
            "unit": "ns",
            "range": "± 2.9118285088319835"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2718.919926570012,
            "unit": "ns",
            "range": "± 1.4177394494204345"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 85614.49573625837,
            "unit": "ns",
            "range": "± 364.3490033260248"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 24997.471616472518,
            "unit": "ns",
            "range": "± 26.74956061317253"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 26015.546315511066,
            "unit": "ns",
            "range": "± 68.77582969001682"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 98823.00846980169,
            "unit": "ns",
            "range": "± 160.80895179637903"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 33787.9505051833,
            "unit": "ns",
            "range": "± 162.80796683501228"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1619280.9715401786,
            "unit": "ns",
            "range": "± 5962.1843248211535"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 695198.4983473558,
            "unit": "ns",
            "range": "± 1611.9714896794017"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4576.243617718036,
            "unit": "ns",
            "range": "± 6.008106217811217"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1884.7323090479924,
            "unit": "ns",
            "range": "± 0.8708542899614319"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4372.757004801432,
            "unit": "ns",
            "range": "± 2.312746651694786"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1878.875180380685,
            "unit": "ns",
            "range": "± 0.970181067002289"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1880.3629859044001,
            "unit": "ns",
            "range": "± 1.491608987602715"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3163.719253540039,
            "unit": "ns",
            "range": "± 4.187075016964277"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2681.71996483436,
            "unit": "ns",
            "range": "± 8.84479639462484"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 43752.09768442007,
            "unit": "ns",
            "range": "± 13.395158803932716"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 17405.964043753487,
            "unit": "ns",
            "range": "± 24.021805602126786"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18864.720234462195,
            "unit": "ns",
            "range": "± 9.170552703304182"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 37303.19795109676,
            "unit": "ns",
            "range": "± 124.3438573588121"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 34206.20649937221,
            "unit": "ns",
            "range": "± 158.37200527053375"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 441383.2894205729,
            "unit": "ns",
            "range": "± 2231.8046720892826"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 189729.70335170202,
            "unit": "ns",
            "range": "± 90.57339792200185"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4421.919898351033,
            "unit": "ns",
            "range": "± 3.6647718261897433"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73156.01217651367,
            "unit": "ns",
            "range": "± 102.07748815758877"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4412.72763296274,
            "unit": "ns",
            "range": "± 3.10482433615547"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 297769.87679036456,
            "unit": "ns",
            "range": "± 230.6957679834006"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73304.40923200335,
            "unit": "ns",
            "range": "± 82.96731917615575"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3109.648613856389,
            "unit": "ns",
            "range": "± 6.491507248584982"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2706.7321461995443,
            "unit": "ns",
            "range": "± 1.6289685301795434"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 44601.968200683594,
            "unit": "ns",
            "range": "± 16.415109769713627"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 30603752.911458332,
            "unit": "ns",
            "range": "± 17767.542836080796"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 710133.6000600961,
            "unit": "ns",
            "range": "± 512.9163456340627"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 37044.25015055339,
            "unit": "ns",
            "range": "± 107.20742443759825"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 34388.23081752232,
            "unit": "ns",
            "range": "± 465.161091436171"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 485329.8983677455,
            "unit": "ns",
            "range": "± 310.08064697618454"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2007538701.8,
            "unit": "ns",
            "range": "± 4641548.634630429"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4370.063733614408,
            "unit": "ns",
            "range": "± 2.668931230946191"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 1879.7464059682993,
            "unit": "ns",
            "range": "± 2.497574449204179"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 322416.3503417969,
            "unit": "ns",
            "range": "± 104.66854132330481"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3171.867734273275,
            "unit": "ns",
            "range": "± 5.175136796147131"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2606.794021313007,
            "unit": "ns",
            "range": "± 4.575110155910552"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 57089.446911151594,
            "unit": "ns",
            "range": "± 102.74540411589324"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 17338.452318827312,
            "unit": "ns",
            "range": "± 17.250012976920964"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31287376.514423076,
            "unit": "ns",
            "range": "± 27761.510355107028"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 37471.663626534595,
            "unit": "ns",
            "range": "± 158.38397989272926"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 34265.7966647678,
            "unit": "ns",
            "range": "± 717.254798616494"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 16739562.264160156,
            "unit": "ns",
            "range": "± 768961.846099572"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 8298899.052083333,
            "unit": "ns",
            "range": "± 27664.218895841546"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 8538503.35044643,
            "unit": "ns",
            "range": "± 113202.22586647469"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 217872305.56944445,
            "unit": "ns",
            "range": "± 12970027.597276667"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 124769885.84615384,
            "unit": "ns",
            "range": "± 3267614.373508003"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 130259031.55833334,
            "unit": "ns",
            "range": "± 3894052.2929705693"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 19778475.62006579,
            "unit": "ns",
            "range": "± 848812.4728671693"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 17583247.54880137,
            "unit": "ns",
            "range": "± 871015.7571396792"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 243309110.10000005,
            "unit": "ns",
            "range": "± 8487310.029500095"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 151230576.9375,
            "unit": "ns",
            "range": "± 3481046.2110995743"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1024)",
            "value": 98.98093225405766,
            "unit": "ns",
            "range": "± 0.10506253449035517"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1024)",
            "value": 681.6714813368661,
            "unit": "ns",
            "range": "± 0.8375479078402603"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1024)",
            "value": 96.90209254196712,
            "unit": "ns",
            "range": "± 0.3197515967504783"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1000000)",
            "value": 133553.3131197416,
            "unit": "ns",
            "range": "± 203.49883457280916"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1000000)",
            "value": 627763.0832170759,
            "unit": "ns",
            "range": "± 985.762756343942"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1000000)",
            "value": 80290.16213553293,
            "unit": "ns",
            "range": "± 288.2862662969831"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7644.8202718098955,
            "unit": "ns",
            "range": "± 1.6705825106961418"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2565.5828343904936,
            "unit": "ns",
            "range": "± 1.4891532152709004"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7641.280045645578,
            "unit": "ns",
            "range": "± 1.7621023214366216"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2569.988443647112,
            "unit": "ns",
            "range": "± 5.907646486118309"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8916.792883736747,
            "unit": "ns",
            "range": "± 2.55093399635208"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 9510.502916776217,
            "unit": "ns",
            "range": "± 7.1832848038986326"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8917.86850797213,
            "unit": "ns",
            "range": "± 2.659723475532359"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 9441.506131685697,
            "unit": "ns",
            "range": "± 21.036949657632103"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 34963.29087477464,
            "unit": "ns",
            "range": "± 9.976617357387262"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 20359.795207432337,
            "unit": "ns",
            "range": "± 75.2769286609415"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 13681.64005025228,
            "unit": "ns",
            "range": "± 40.61127335570531"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 8188.09106648763,
            "unit": "ns",
            "range": "± 104.99579841185611"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 6391.746244430542,
            "unit": "ns",
            "range": "± 6.671566395162595"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 6392.658172607422,
            "unit": "ns",
            "range": "± 7.159010381840794"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 14030.87691791241,
            "unit": "ns",
            "range": "± 11.351181815012884"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 33589.89025442941,
            "unit": "ns",
            "range": "± 25.23937097688062"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 26355.24669756208,
            "unit": "ns",
            "range": "± 110.99363578610557"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 14998.3154296875,
            "unit": "ns",
            "range": "± 1.8842643301427018"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 8950.323622483473,
            "unit": "ns",
            "range": "± 11.88413823156657"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5122.3454927297735,
            "unit": "ns",
            "range": "± 8.242880407577193"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5121.966491699219,
            "unit": "ns",
            "range": "± 5.829436053684548"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 12149.770158034105,
            "unit": "ns",
            "range": "± 4.6123653418985295"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 92827.54846191406,
            "unit": "ns",
            "range": "± 40.88459824561964"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 20007.921543666296,
            "unit": "ns",
            "range": "± 30.86274474741776"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 13413.931077223559,
            "unit": "ns",
            "range": "± 1.3614966789871399"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 9153.708289010185,
            "unit": "ns",
            "range": "± 2.0941455706417247"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4250.702863839956,
            "unit": "ns",
            "range": "± 2.205536611949784"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4103.406438974233,
            "unit": "ns",
            "range": "± 3.432613409469458"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 11474.693152109781,
            "unit": "ns",
            "range": "± 5.171192582962528"
          },
          {
            "name": "BitPackingBenchmark.Pack_BclBitArray",
            "value": 280414.643484933,
            "unit": "ns",
            "range": "± 96.04623681209415"
          },
          {
            "name": "BitPackingBenchmark.Pack_BitWriter",
            "value": 25233.6488571167,
            "unit": "ns",
            "range": "± 15.272633774912343"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 1024)",
            "value": 1048.0801525115967,
            "unit": "ns",
            "range": "± 6.758745026144857"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 1024)",
            "value": 7.31066205004851,
            "unit": "ns",
            "range": "± 0.12446369291198188"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 65536)",
            "value": 172398.71232722356,
            "unit": "ns",
            "range": "± 147.71186874991727"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 65536)",
            "value": 328.2518652402438,
            "unit": "ns",
            "range": "± 0.26319753222801895"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Predictable(Length: 1000000)",
            "value": 627141.812639509,
            "unit": "ns",
            "range": "± 437.935035950129"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Predictable(Length: 1000000)",
            "value": 942452.993765024,
            "unit": "ns",
            "range": "± 963.8263123105162"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1932283.909352022,
            "unit": "ns",
            "range": "± 37124.44941182525"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 2962924.9928385415,
            "unit": "ns",
            "range": "± 61455.74284949797"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2934633.888454861,
            "unit": "ns",
            "range": "± 60458.836989956304"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 1024)",
            "value": 1037.9601877757482,
            "unit": "ns",
            "range": "± 3.9842617598337635"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 1024)",
            "value": 822.6003777640207,
            "unit": "ns",
            "range": "± 1.1022156641269703"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 65536)",
            "value": 172403.05062430244,
            "unit": "ns",
            "range": "± 197.65910239893452"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 65536)",
            "value": 55050.59997089092,
            "unit": "ns",
            "range": "± 13.193331750427678"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 1024)",
            "value": 284.7387353181839,
            "unit": "ns",
            "range": "± 0.14512924369265587"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 1024)",
            "value": 244.88920685450236,
            "unit": "ns",
            "range": "± 0.1495870994930527"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 65536)",
            "value": 15798.353522667518,
            "unit": "ns",
            "range": "± 10.424619751697321"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 65536)",
            "value": 15430.431470598493,
            "unit": "ns",
            "range": "± 24.80441610257642"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 23027360.4575,
            "unit": "ns",
            "range": "± 5557592.370914973"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3262570.516183036,
            "unit": "ns",
            "range": "± 8357.19017347261"
          },
          {
            "name": "BitPackingBenchmark.Unpack_BclBitArray",
            "value": 325402.2047400841,
            "unit": "ns",
            "range": "± 147.457861734798"
          },
          {
            "name": "BitPackingBenchmark.Unpack_BitReader",
            "value": 21200.651072184246,
            "unit": "ns",
            "range": "± 4.946950883721505"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Unpredictable(Length: 1000000)",
            "value": 4623476.850911458,
            "unit": "ns",
            "range": "± 882.9623175776024"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Unpredictable(Length: 1000000)",
            "value": 942063.3579915365,
            "unit": "ns",
            "range": "± 581.6484859145038"
          },
          {
            "name": "GuidBenchmark.V4_BclNewGuid",
            "value": 2720332.490625,
            "unit": "ns",
            "range": "± 7026.0420624447515"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_Xoshiro",
            "value": 79206.64184570312,
            "unit": "ns",
            "range": "± 27.63942984150911"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_WyRand",
            "value": 72762.46126883371,
            "unit": "ns",
            "range": "± 98.73743436546731"
          },
          {
            "name": "GuidBenchmark.V7_BclNewGuid",
            "value": 2743564.6334134615,
            "unit": "ns",
            "range": "± 1441.9600483776817"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Stateless",
            "value": 68635.33687046597,
            "unit": "ns",
            "range": "± 99.9771764997422"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Monotonic",
            "value": 62538.20090157645,
            "unit": "ns",
            "range": "± 49.2279591076058"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 13570830.352941176,
            "unit": "ns",
            "range": "± 428956.3605323325"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5814245.347826087,
            "unit": "ns",
            "range": "± 136769.44861436097"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5905772.44,
            "unit": "ns",
            "range": "± 237449.28427360512"
          }
        ]
      },
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
          "id": "bfa1bcd039bd7495e741b76a358b125e53355096",
          "message": "Merge pull request #262 from marius-bughiu/claude/showcase-benchmarks-docs\n\nShowcase-package benchmarks + docs-site section (follow-up to #261)",
          "timestamp": "2026-07-11T19:27:03Z",
          "url": "https://github.com/marius-bughiu/Celerity/commit/bfa1bcd039bd7495e741b76a358b125e53355096"
        },
        "date": 1783938372985,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "RingBenchmark.Md5Ring_GetNode",
            "value": 3392128.0954241073,
            "unit": "ns",
            "range": "± 3304.8477009858007"
          },
          {
            "name": "RingBenchmark.CelerityRing_GetNode",
            "value": 347749.93107722356,
            "unit": "ns",
            "range": "± 120.57170769145297"
          },
          {
            "name": "SentinelBenchmark.Exact_DictionaryReport(DistinctKeys: 10000)",
            "value": 1200279.7076822917,
            "unit": "ns",
            "range": "± 9153.194347416087"
          },
          {
            "name": "SentinelBenchmark.Sketch_AbuseTrackerReport(DistinctKeys: 10000)",
            "value": 6998356.589285715,
            "unit": "ns",
            "range": "± 25711.091982654874"
          },
          {
            "name": "CardinalityBenchmark.HashSet_DistinctCount(Cardinality: 100000)",
            "value": 4529850.518841912,
            "unit": "ns",
            "range": "± 133139.3304929066"
          },
          {
            "name": "CardinalityBenchmark.Distinct_DistinctCount(Cardinality: 100000)",
            "value": 2012375.501953125,
            "unit": "ns",
            "range": "± 1748.7969618996897"
          },
          {
            "name": "SentinelBenchmark.Exact_DictionaryReport(DistinctKeys: 100000)",
            "value": 16902640.5565625,
            "unit": "ns",
            "range": "± 1088568.593071962"
          },
          {
            "name": "SentinelBenchmark.Sketch_AbuseTrackerReport(DistinctKeys: 100000)",
            "value": 75196649.19387755,
            "unit": "ns",
            "range": "± 528318.2639849754"
          },
          {
            "name": "CardinalityBenchmark.HashSet_DistinctCount(Cardinality: 1000000)",
            "value": 51816579.33225807,
            "unit": "ns",
            "range": "± 1567815.4129335433"
          },
          {
            "name": "CardinalityBenchmark.Distinct_DistinctCount(Cardinality: 1000000)",
            "value": 15260922.296875,
            "unit": "ns",
            "range": "± 28667.742068369167"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1024)",
            "value": 342.9111286799113,
            "unit": "ns",
            "range": "± 2.188468052199611"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1024)",
            "value": 32.11790365832193,
            "unit": "ns",
            "range": "± 0.18387511721238273"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1024)",
            "value": 142.23850576082864,
            "unit": "ns",
            "range": "± 0.25316491927702234"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_ScalarCheckedLoop(Length: 1000000)",
            "value": 325993.6757463728,
            "unit": "ns",
            "range": "± 3357.2448930989704"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_TensorPrimitivesUnchecked(Length: 1000000)",
            "value": 50398.38966471354,
            "unit": "ns",
            "range": "± 47.267693201929305"
          },
          {
            "name": "SimdReductionsBenchmark.CheckedSum_SimdReductions(Length: 1000000)",
            "value": 139352.70226111778,
            "unit": "ns",
            "range": "± 73.25207710058066"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1703942.4002511161,
            "unit": "ns",
            "range": "± 13832.942381257879"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 753238.6071920956,
            "unit": "ns",
            "range": "± 15465.436599350684"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 1)",
            "value": 1798617.358528646,
            "unit": "ns",
            "range": "± 10927.316817050167"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2447434.68984375,
            "unit": "ns",
            "range": "± 21960.582521944725"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 1147390.23359375,
            "unit": "ns",
            "range": "± 13544.502045833093"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 4)",
            "value": 2533713.8842447917,
            "unit": "ns",
            "range": "± 22890.922944110538"
          },
          {
            "name": "ConcurrentAccessBenchmark.Dictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4985831.5707236845,
            "unit": "ns",
            "range": "± 109293.0892629159"
          },
          {
            "name": "ConcurrentAccessBenchmark.IntDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 2281034.5302083334,
            "unit": "ns",
            "range": "± 27591.644449079307"
          },
          {
            "name": "ConcurrentAccessBenchmark.ConcurrentDictionary_ConcurrentLookup(ItemCount: 100000, ThreadCount: 8)",
            "value": 4728627.979867788,
            "unit": "ns",
            "range": "± 123250.2697027157"
          },
          {
            "name": "VarIntBenchmark.Decode_BclBinaryReader",
            "value": 74666.74181315103,
            "unit": "ns",
            "range": "± 112.7839133307735"
          },
          {
            "name": "VarIntBenchmark.Decode_VarIntSpan",
            "value": 27807.542185465496,
            "unit": "ns",
            "range": "± 1010.0330553920652"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 13279.346887376574,
            "unit": "ns",
            "range": "± 272.0906386230396"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 6799.502159705529,
            "unit": "ns",
            "range": "± 35.37515941227458"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3690514.3018043153,
            "unit": "ns",
            "range": "± 134332.35345982783"
          },
          {
            "name": "EnsureCapacityBenchmark.Dictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1886956.5811941964,
            "unit": "ns",
            "range": "± 42753.97648626346"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_NaiveLoop",
            "value": 29979.099605853742,
            "unit": "ns",
            "range": "± 18.756952161298347"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_MathLog10",
            "value": 42262.60457904522,
            "unit": "ns",
            "range": "± 25.76318589373433"
          },
          {
            "name": "CountDigitsBenchmark.Digits32_FastUtils",
            "value": 3348.3057931264243,
            "unit": "ns",
            "range": "± 0.9668829883714236"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_NaiveLoop",
            "value": 99416.9504045759,
            "unit": "ns",
            "range": "± 80.88229486949076"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_MathLog10",
            "value": 41949.35949707031,
            "unit": "ns",
            "range": "± 19.025680494036862"
          },
          {
            "name": "CountDigitsBenchmark.Digits64_FastUtils",
            "value": 8385.601314838115,
            "unit": "ns",
            "range": "± 8.251411732658312"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7644.269201006208,
            "unit": "ns",
            "range": "± 2.988330499308045"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2975.386050860087,
            "unit": "ns",
            "range": "± 0.7190015736555317"
          },
          {
            "name": "FastModBenchmark.Div32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7641.688947405134,
            "unit": "ns",
            "range": "± 1.2854374957862604"
          },
          {
            "name": "FastModBenchmark.Div32_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2981.77326801845,
            "unit": "ns",
            "range": "± 1.1896507611473115"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8917.86357820951,
            "unit": "ns",
            "range": "± 1.2908492636483384"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 97, Divisor64: 1000000007)",
            "value": 6419.933816469633,
            "unit": "ns",
            "range": "± 3.7292234908204196"
          },
          {
            "name": "FastModBenchmark.Div64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8918.227591959636,
            "unit": "ns",
            "range": "± 2.615450915514817"
          },
          {
            "name": "FastModBenchmark.Div64_FastDiv(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 6462.791849772136,
            "unit": "ns",
            "range": "± 1.0412059030042062"
          },
          {
            "name": "VarIntBenchmark.Encode_BclBinaryWriter",
            "value": 87061.56135253907,
            "unit": "ns",
            "range": "± 992.4226793273182"
          },
          {
            "name": "VarIntBenchmark.Encode_VarIntSpan",
            "value": 22208.91102164132,
            "unit": "ns",
            "range": "± 26.39234342318006"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_FromCollection(ItemCount: 100000)",
            "value": 959682.8266992187,
            "unit": "ns",
            "range": "± 87205.85638069673"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_FromCollection(ItemCount: 100000)",
            "value": 817539.6261067708,
            "unit": "ns",
            "range": "± 9885.55302390146"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_FromCollection(ItemCount: 100000)",
            "value": 824753.7919921875,
            "unit": "ns",
            "range": "± 8911.26577091123"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Grow(ItemCount: 100000)",
            "value": 4443059.451523437,
            "unit": "ns",
            "range": "± 801065.3887861252"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Grow(ItemCount: 100000)",
            "value": 4923361.537259615,
            "unit": "ns",
            "range": "± 128567.14225600494"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Grow(ItemCount: 100000)",
            "value": 4950955.682291667,
            "unit": "ns",
            "range": "± 75726.2464495447"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_InOrder(ItemCount: 1000000)",
            "value": 4394130.506138393,
            "unit": "ns",
            "range": "± 2432.673256872586"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_InOrder(ItemCount: 1000000)",
            "value": 1888940.3688401442,
            "unit": "ns",
            "range": "± 822.8768868441266"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 13722.37349482945,
            "unit": "ns",
            "range": "± 193.84156295884495"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6754.548822843111,
            "unit": "ns",
            "range": "± 32.13392304213119"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 11791.240037536621,
            "unit": "ns",
            "range": "± 92.58474465298292"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6276.628606669108,
            "unit": "ns",
            "range": "± 89.06094600746033"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 6095.413356781006,
            "unit": "ns",
            "range": "± 68.30768544926872"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7332.709771292551,
            "unit": "ns",
            "range": "± 50.21266671976745"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 1000)",
            "value": 7525.250542413621,
            "unit": "ns",
            "range": "± 269.94227509595373"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 151805.8380533854,
            "unit": "ns",
            "range": "± 633.0750287944043"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 38060.71225914588,
            "unit": "ns",
            "range": "± 262.12528010641495"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 38631.66777256557,
            "unit": "ns",
            "range": "± 488.3386639589927"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 44322.604024251305,
            "unit": "ns",
            "range": "± 232.06654070257557"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Uniform, ItemCount: 10000)",
            "value": 42042.283040364586,
            "unit": "ns",
            "range": "± 284.66435837839066"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 3914850.87109375,
            "unit": "ns",
            "range": "± 17160.22752782826"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Uniform, ItemCount: 100000)",
            "value": 4688528.628348215,
            "unit": "ns",
            "range": "± 16529.36889774161"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 12771.795875549316,
            "unit": "ns",
            "range": "± 96.49582169635785"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6664.207041931152,
            "unit": "ns",
            "range": "± 87.21291113112756"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 9290.355368041992,
            "unit": "ns",
            "range": "± 108.42924608791435"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 4961.431923421224,
            "unit": "ns",
            "range": "± 83.16076902205938"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 5309.941539057979,
            "unit": "ns",
            "range": "± 146.9953271757152"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 7220.899017878941,
            "unit": "ns",
            "range": "± 85.71241816096786"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 1000)",
            "value": 6763.040924521054,
            "unit": "ns",
            "range": "± 133.08925740723785"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 137174.18090820312,
            "unit": "ns",
            "range": "± 729.3738215116258"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 29036.3064259847,
            "unit": "ns",
            "range": "± 160.39655143691922"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 30799.166423797607,
            "unit": "ns",
            "range": "± 585.0147675295375"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 44497.71720784505,
            "unit": "ns",
            "range": "± 308.8284804031816"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Sequential, ItemCount: 10000)",
            "value": 42628.17666015625,
            "unit": "ns",
            "range": "± 338.92421063084976"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 3274798.9332421874,
            "unit": "ns",
            "range": "± 275873.0565629846"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Sequential, ItemCount: 100000)",
            "value": 2113710.028862847,
            "unit": "ns",
            "range": "± 44004.99063006947"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 15890.00997052874,
            "unit": "ns",
            "range": "± 277.9946903791446"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 6722.966159057617,
            "unit": "ns",
            "range": "± 76.13291511187339"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 229152.8344203404,
            "unit": "ns",
            "range": "± 353.76568767910254"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 474710.9174429086,
            "unit": "ns",
            "range": "± 153.0227466036634"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 165437.9344951923,
            "unit": "ns",
            "range": "± 98.15160368887517"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7299.17902692159,
            "unit": "ns",
            "range": "± 37.38428800606578"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 1000)",
            "value": 7110.171003129747,
            "unit": "ns",
            "range": "± 149.2664133963915"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 138406.6100341797,
            "unit": "ns",
            "range": "± 582.5062943610243"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 30599447.213942308,
            "unit": "ns",
            "range": "± 13584.390002542841"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 721902.1244419643,
            "unit": "ns",
            "range": "± 688.9280428131955"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 44538.58800252279,
            "unit": "ns",
            "range": "± 520.8147136755553"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Clustered, ItemCount: 10000)",
            "value": 42443.00770263672,
            "unit": "ns",
            "range": "± 361.2705855011479"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 3731838.67890625,
            "unit": "ns",
            "range": "± 295616.20087802026"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Insert(Distribution: Clustered, ItemCount: 100000)",
            "value": 4601559887.714286,
            "unit": "ns",
            "range": "± 1185058.8296300173"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6709.567382303873,
            "unit": "ns",
            "range": "± 57.978400224353805"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 5330.633516947429,
            "unit": "ns",
            "range": "± 131.68760190557938"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 515886.5305363582,
            "unit": "ns",
            "range": "± 386.25546434649306"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 7603.988490804037,
            "unit": "ns",
            "range": "± 107.02740600250445"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 1000)",
            "value": 6878.027834510804,
            "unit": "ns",
            "range": "± 229.60237098579384"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 138877.93090820312,
            "unit": "ns",
            "range": "± 682.4725028605008"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 28922.391721452987,
            "unit": "ns",
            "range": "± 242.93591619896654"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31285229.278846152,
            "unit": "ns",
            "range": "± 16244.180931395436"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44570.519784109936,
            "unit": "ns",
            "range": "± 281.2779827500546"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Insert(Distribution: Adversarial, ItemCount: 10000)",
            "value": 42412.908215332034,
            "unit": "ns",
            "range": "± 487.1842020040089"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 1000000)",
            "value": 19797295.02232143,
            "unit": "ns",
            "range": "± 181246.5620144982"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 1000000)",
            "value": 23544138.654296875,
            "unit": "ns",
            "range": "± 380461.35146970995"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 1000000)",
            "value": 23641180.184375,
            "unit": "ns",
            "range": "± 141776.3866869975"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Int(ItemCount: 5000000)",
            "value": 261342285.85185185,
            "unit": "ns",
            "range": "± 7165502.873920334"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Insert(ItemCount: 5000000)",
            "value": 150639469.64285713,
            "unit": "ns",
            "range": "± 3441110.6859708787"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Insert(ItemCount: 5000000)",
            "value": 151216438.65714285,
            "unit": "ns",
            "range": "± 4020004.115368022"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 1000000)",
            "value": 23260332.96625,
            "unit": "ns",
            "range": "± 611238.4163351163"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 1000000)",
            "value": 30429597.741666667,
            "unit": "ns",
            "range": "± 563892.1247177698"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Insert_Long(ItemCount: 5000000)",
            "value": 274733452.9,
            "unit": "ns",
            "range": "± 4994857.822852419"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Insert(ItemCount: 5000000)",
            "value": 165958730.4740741,
            "unit": "ns",
            "range": "± 6238537.547868564"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 1000)",
            "value": 11066.066265360514,
            "unit": "ns",
            "range": "± 138.71243982866778"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 1000)",
            "value": 3347.5385085514613,
            "unit": "ns",
            "range": "± 29.467114883264742"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_Unsized(ItemCount: 100000)",
            "value": 3420533.327864583,
            "unit": "ns",
            "range": "± 36781.74975305103"
          },
          {
            "name": "EnsureCapacityBenchmark.IntDictionary_Insert_EnsureCapacity(ItemCount: 100000)",
            "value": 1300117.5092377535,
            "unit": "ns",
            "range": "± 44105.71417382479"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4370.651457566481,
            "unit": "ns",
            "range": "± 3.195824679646527"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 1000)",
            "value": 4787.410220554897,
            "unit": "ns",
            "range": "± 6.983999695629996"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 1000)",
            "value": 322776.7440091647,
            "unit": "ns",
            "range": "± 600.5072951532173"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 1000)",
            "value": 2981.496482849121,
            "unit": "ns",
            "range": "± 4.24778584656853"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 1000)",
            "value": 2658.878624725342,
            "unit": "ns",
            "range": "± 4.67751783586294"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 1000)",
            "value": 2163.814556121826,
            "unit": "ns",
            "range": "± 4.2730278382337135"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 1000)",
            "value": 2491.210171254476,
            "unit": "ns",
            "range": "± 18.89336848915888"
          },
          {
            "name": "AdversarialHasherBenchmark.Dictionary_Lookup(ItemCount: 10000)",
            "value": 44298.75166907677,
            "unit": "ns",
            "range": "± 19.367677773516718"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Naive_Lookup(ItemCount: 10000)",
            "value": 31309940.166666668,
            "unit": "ns",
            "range": "± 18272.122055490658"
          },
          {
            "name": "AdversarialHasherBenchmark.IntDictionary_Murmur3_Lookup(ItemCount: 10000)",
            "value": 34399.81985677083,
            "unit": "ns",
            "range": "± 83.34217028179654"
          },
          {
            "name": "LibraryComparisonBenchmark.Dictionary_Lookup(ItemCount: 100000)",
            "value": 1621637.39609375,
            "unit": "ns",
            "range": "± 1689.9053003646206"
          },
          {
            "name": "LibraryComparisonBenchmark.FrozenDictionary_Lookup(ItemCount: 100000)",
            "value": 974338.6768275669,
            "unit": "ns",
            "range": "± 5422.539176554533"
          },
          {
            "name": "LibraryComparisonBenchmark.IntDictionary_Lookup(ItemCount: 100000)",
            "value": 716313.2220284598,
            "unit": "ns",
            "range": "± 2392.346355436599"
          },
          {
            "name": "LibraryComparisonBenchmark.CelerityDictionary_Lookup(ItemCount: 100000)",
            "value": 709666.1775251116,
            "unit": "ns",
            "range": "± 1050.2718834498496"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4752.311856587728,
            "unit": "ns",
            "range": "± 10.785907604253035"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2170.422099304199,
            "unit": "ns",
            "range": "± 7.382357287085278"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 4778.00362701416,
            "unit": "ns",
            "range": "± 12.6570269688122"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2007.0293491070088,
            "unit": "ns",
            "range": "± 4.611341982750984"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2297.049071720668,
            "unit": "ns",
            "range": "± 18.881386618154433"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 3151.50223719279,
            "unit": "ns",
            "range": "± 4.177148958648531"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 1000)",
            "value": 2719.3905624976524,
            "unit": "ns",
            "range": "± 1.4474126019310898"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 85642.10015399639,
            "unit": "ns",
            "range": "± 220.46628510051494"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 25083.45832519531,
            "unit": "ns",
            "range": "± 44.5291208249674"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 26406.233461652482,
            "unit": "ns",
            "range": "± 183.06068655591494"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 36803.07744489397,
            "unit": "ns",
            "range": "± 21.658538520165635"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Uniform, ItemCount: 10000)",
            "value": 33609.24749319894,
            "unit": "ns",
            "range": "± 117.9539602295943"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 1621109.1946614583,
            "unit": "ns",
            "range": "± 4676.9845743367805"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Uniform, ItemCount: 100000)",
            "value": 688487.7891276042,
            "unit": "ns",
            "range": "± 2370.5640015724443"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4373.470417567662,
            "unit": "ns",
            "range": "± 1.7662672327574782"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1882.2707463777983,
            "unit": "ns",
            "range": "± 1.0920559295429668"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 4370.089862823486,
            "unit": "ns",
            "range": "± 1.2139441479634665"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1910.5348346416768,
            "unit": "ns",
            "range": "± 0.7700430301921478"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 1880.7203427723475,
            "unit": "ns",
            "range": "± 1.0277937954182692"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 3245.2062903812953,
            "unit": "ns",
            "range": "± 1.602117436149439"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 1000)",
            "value": 2646.5583101908364,
            "unit": "ns",
            "range": "± 8.799718726169527"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 45383.853031412764,
            "unit": "ns",
            "range": "± 64.5538174722134"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 17298.0512172154,
            "unit": "ns",
            "range": "± 9.498603529480397"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 18828.72549874442,
            "unit": "ns",
            "range": "± 7.724269418784646"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 37234.00690133231,
            "unit": "ns",
            "range": "± 115.5261397571668"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Sequential, ItemCount: 10000)",
            "value": 33963.16023036412,
            "unit": "ns",
            "range": "± 273.81094096997833"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 439646.46627371653,
            "unit": "ns",
            "range": "± 193.87657400523145"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Sequential, ItemCount: 100000)",
            "value": 200906.97481863838,
            "unit": "ns",
            "range": "± 279.1903994499278"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4414.218931344839,
            "unit": "ns",
            "range": "± 1.494881112151652"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73115.02354939778,
            "unit": "ns",
            "range": "± 31.684936077641982"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 4412.4331459632285,
            "unit": "ns",
            "range": "± 3.1300063912643914"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 297736.6551983173,
            "unit": "ns",
            "range": "± 60.62119429000341"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 73324.21965738932,
            "unit": "ns",
            "range": "± 69.05626812271942"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 3115.64273752485,
            "unit": "ns",
            "range": "± 6.236345380775702"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 1000)",
            "value": 2708.1223088777983,
            "unit": "ns",
            "range": "± 2.3602867082678842"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 44677.7762058803,
            "unit": "ns",
            "range": "± 13.930728330751297"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 30627299.8671875,
            "unit": "ns",
            "range": "± 24685.989271922524"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 710897.066781851,
            "unit": "ns",
            "range": "± 541.6843496008586"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 36935.775525774276,
            "unit": "ns",
            "range": "± 106.46533521119457"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Clustered, ItemCount: 10000)",
            "value": 34233.78311861478,
            "unit": "ns",
            "range": "± 251.32227583660176"
          },
          {
            "name": "DistributionBenchmark.Dictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 485182.4718299279,
            "unit": "ns",
            "range": "± 70.14981251272178"
          },
          {
            "name": "DistributionBenchmark.IntDictionary_Lookup(Distribution: Clustered, ItemCount: 100000)",
            "value": 2005246386.142857,
            "unit": "ns",
            "range": "± 1426342.0718914766"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 4529.407100041707,
            "unit": "ns",
            "range": "± 2.4568533156883245"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 1891.163895062038,
            "unit": "ns",
            "range": "± 1.6588433787187309"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 322545.31278483075,
            "unit": "ns",
            "range": "± 393.41049681949687"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 3158.733561515808,
            "unit": "ns",
            "range": "± 0.6923024983042464"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 1000)",
            "value": 2634.419083731515,
            "unit": "ns",
            "range": "± 7.5690149762226735"
          },
          {
            "name": "HasherEndToEndBenchmark.Dictionary_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 44371.86970872145,
            "unit": "ns",
            "range": "± 20.35367578609622"
          },
          {
            "name": "HasherEndToEndBenchmark.Identity_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 17308.661295964168,
            "unit": "ns",
            "range": "± 9.262032490571467"
          },
          {
            "name": "HasherEndToEndBenchmark.Naive_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 31272059.85096154,
            "unit": "ns",
            "range": "± 9191.42237762569"
          },
          {
            "name": "HasherEndToEndBenchmark.Wang_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 37292.34893362863,
            "unit": "ns",
            "range": "± 160.91380477115447"
          },
          {
            "name": "HasherEndToEndBenchmark.Murmur3_Lookup(Distribution: Adversarial, ItemCount: 10000)",
            "value": 34250.43609212239,
            "unit": "ns",
            "range": "± 368.84189560017904"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 1000000)",
            "value": 17378874.8046875,
            "unit": "ns",
            "range": "± 532749.2922592049"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 1000000)",
            "value": 8665512.481971154,
            "unit": "ns",
            "range": "± 121553.99820887584"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 1000000)",
            "value": 9271663.7859375,
            "unit": "ns",
            "range": "± 212124.26132930088"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Int(ItemCount: 5000000)",
            "value": 244214101.25333333,
            "unit": "ns",
            "range": "± 12135478.831552228"
          },
          {
            "name": "LargeDatasetBenchmark.IntDictionary_Lookup(ItemCount: 5000000)",
            "value": 135518893.27083334,
            "unit": "ns",
            "range": "± 3423317.1553485766"
          },
          {
            "name": "LargeDatasetBenchmark.CelerityDictionary_Lookup(ItemCount: 5000000)",
            "value": 138639562.33333334,
            "unit": "ns",
            "range": "± 2578979.0848576105"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 1000000)",
            "value": 21525287.929924242,
            "unit": "ns",
            "range": "± 1002499.6222671322"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 1000000)",
            "value": 17451045.393292684,
            "unit": "ns",
            "range": "± 592833.6710471374"
          },
          {
            "name": "LargeDatasetBenchmark.Dictionary_Lookup_Long(ItemCount: 5000000)",
            "value": 248801147.825,
            "unit": "ns",
            "range": "± 5597364.415467912"
          },
          {
            "name": "LargeDatasetBenchmark.LongDictionary_Lookup(ItemCount: 5000000)",
            "value": 166237909.25925925,
            "unit": "ns",
            "range": "± 3096933.855573488"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1024)",
            "value": 100.52356141408285,
            "unit": "ns",
            "range": "± 0.7423101672594679"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1024)",
            "value": 689.8057396570841,
            "unit": "ns",
            "range": "± 2.634699538915197"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1024)",
            "value": 96.64092862606049,
            "unit": "ns",
            "range": "± 0.018089098224741983"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_TensorPrimitives_TwoPass(Length: 1000000)",
            "value": 134240.5170288086,
            "unit": "ns",
            "range": "± 260.0844723519718"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_NaiveScalarLoop(Length: 1000000)",
            "value": 628907.7961588542,
            "unit": "ns",
            "range": "± 1791.1926259771735"
          },
          {
            "name": "SimdReductionsBenchmark.MinMax_SimdReductions(Length: 1000000)",
            "value": 79698.76497977121,
            "unit": "ns",
            "range": "± 131.5402712260544"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 7644.816084725516,
            "unit": "ns",
            "range": "± 1.2189002012600822"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 2581.258338634784,
            "unit": "ns",
            "range": "± 3.0522931928937385"
          },
          {
            "name": "FastModBenchmark.Mod32_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 7642.625970693735,
            "unit": "ns",
            "range": "± 1.463598610556791"
          },
          {
            "name": "FastModBenchmark.Mod32_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 2579.2021305377666,
            "unit": "ns",
            "range": "± 4.1753423011126065"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 97, Divisor64: 1000000007)",
            "value": 8917.571807861328,
            "unit": "ns",
            "range": "± 1.2386173009215962"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 97, Divisor64: 1000000007)",
            "value": 9452.911225538988,
            "unit": "ns",
            "range": "± 3.3371398040779403"
          },
          {
            "name": "FastModBenchmark.Mod64_Operator(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 8919.14044494629,
            "unit": "ns",
            "range": "± 2.326693244612168"
          },
          {
            "name": "FastModBenchmark.Mod64_FastMod(Divisor32: 1000, Divisor64: 1000000007)",
            "value": 9467.984643118722,
            "unit": "ns",
            "range": "± 5.211664180188636"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemSeeded",
            "value": 34702.1599214994,
            "unit": "ns",
            "range": "± 7.42212393903044"
          },
          {
            "name": "PrngBenchmark.NextBounded_SystemShared",
            "value": 20359.47297014509,
            "unit": "ns",
            "range": "± 20.646590454735225"
          },
          {
            "name": "PrngBenchmark.NextBounded_SplitMix64",
            "value": 13742.72513286884,
            "unit": "ns",
            "range": "± 193.5097741796382"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoshiro256StarStar",
            "value": 8185.264341227214,
            "unit": "ns",
            "range": "± 101.7450311688377"
          },
          {
            "name": "PrngBenchmark.NextBounded_Xoroshiro128Plus",
            "value": 6382.744306437175,
            "unit": "ns",
            "range": "± 5.030565663630221"
          },
          {
            "name": "PrngBenchmark.NextBounded_WyRand",
            "value": 6385.937655312674,
            "unit": "ns",
            "range": "± 4.984416569234028"
          },
          {
            "name": "PrngBenchmark.NextBounded_Pcg32",
            "value": 12907.444701639812,
            "unit": "ns",
            "range": "± 16.6080250100335"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemSeeded",
            "value": 33611.58464402419,
            "unit": "ns",
            "range": "± 12.337716140879671"
          },
          {
            "name": "PrngBenchmark.NextDouble_SystemShared",
            "value": 26081.505169208234,
            "unit": "ns",
            "range": "± 44.8794298675424"
          },
          {
            "name": "PrngBenchmark.NextDouble_SplitMix64",
            "value": 14993.356187947591,
            "unit": "ns",
            "range": "± 1.904354738046886"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoshiro256StarStar",
            "value": 8940.90112891564,
            "unit": "ns",
            "range": "± 2.5536655174415217"
          },
          {
            "name": "PrngBenchmark.NextDouble_Xoroshiro128Plus",
            "value": 5115.122629438128,
            "unit": "ns",
            "range": "± 4.353623614421404"
          },
          {
            "name": "PrngBenchmark.NextDouble_WyRand",
            "value": 5116.7857950846355,
            "unit": "ns",
            "range": "± 3.334845136820455"
          },
          {
            "name": "PrngBenchmark.NextDouble_Pcg32",
            "value": 12151.510394505092,
            "unit": "ns",
            "range": "± 9.396005859588861"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemSeeded",
            "value": 92826.122267503,
            "unit": "ns",
            "range": "± 49.8629045830468"
          },
          {
            "name": "PrngBenchmark.NextULong_SystemShared",
            "value": 19924.894341102012,
            "unit": "ns",
            "range": "± 28.057247282510506"
          },
          {
            "name": "PrngBenchmark.NextULong_SplitMix64",
            "value": 13412.159084065755,
            "unit": "ns",
            "range": "± 2.027519139190836"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoshiro256StarStar",
            "value": 9153.594815572103,
            "unit": "ns",
            "range": "± 1.367964055099748"
          },
          {
            "name": "PrngBenchmark.NextULong_Xoroshiro128Plus",
            "value": 4266.713440528283,
            "unit": "ns",
            "range": "± 5.223899126886492"
          },
          {
            "name": "PrngBenchmark.NextULong_WyRand",
            "value": 4099.514075142996,
            "unit": "ns",
            "range": "± 1.671003491227568"
          },
          {
            "name": "PrngBenchmark.NextULong_Pcg32",
            "value": 11468.639501718375,
            "unit": "ns",
            "range": "± 2.397124421663625"
          },
          {
            "name": "BitPackingBenchmark.Pack_BclBitArray",
            "value": 280611.592961238,
            "unit": "ns",
            "range": "± 543.9691365811468"
          },
          {
            "name": "BitPackingBenchmark.Pack_BitWriter",
            "value": 25254.86341756185,
            "unit": "ns",
            "range": "± 15.22524757879215"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 1024)",
            "value": 1047.5106691996257,
            "unit": "ns",
            "range": "± 9.51121873902982"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 1024)",
            "value": 7.332286975781122,
            "unit": "ns",
            "range": "± 0.007670388498625497"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_BitArray(BitCount: 65536)",
            "value": 172370.00333658853,
            "unit": "ns",
            "range": "± 353.10560145149486"
          },
          {
            "name": "SpanBitsBenchmark.PopCount_SpanBits(BitCount: 65536)",
            "value": 341.78286610330855,
            "unit": "ns",
            "range": "± 2.7644016922319232"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Predictable(Length: 1000000)",
            "value": 626862.5295973558,
            "unit": "ns",
            "range": "± 433.44686355696683"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Predictable(Length: 1000000)",
            "value": 940305.1553485577,
            "unit": "ns",
            "range": "± 452.96623132247555"
          },
          {
            "name": "MemoryAllocationBenchmark.Dictionary_Presized(ItemCount: 100000)",
            "value": 1915765.4821965145,
            "unit": "ns",
            "range": "± 18684.608922318"
          },
          {
            "name": "MemoryAllocationBenchmark.IntDictionary_Presized(ItemCount: 100000)",
            "value": 2900499.140885417,
            "unit": "ns",
            "range": "± 37874.47226334461"
          },
          {
            "name": "MemoryAllocationBenchmark.CelerityDictionary_Presized(ItemCount: 100000)",
            "value": 2864716.793229167,
            "unit": "ns",
            "range": "± 40681.362953435324"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 1024)",
            "value": 1046.4527508871895,
            "unit": "ns",
            "range": "± 3.732075683891039"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 1024)",
            "value": 821.7751038233439,
            "unit": "ns",
            "range": "± 0.17940345070065222"
          },
          {
            "name": "SpanBitsBenchmark.Scan_BitArray(BitCount: 65536)",
            "value": 172618.06432291667,
            "unit": "ns",
            "range": "± 228.1880653260097"
          },
          {
            "name": "SpanBitsBenchmark.Scan_SpanBits(BitCount: 65536)",
            "value": 55055.191606794084,
            "unit": "ns",
            "range": "± 9.623395320582492"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 1024)",
            "value": 284.5700924213116,
            "unit": "ns",
            "range": "± 0.15637491446454627"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 1024)",
            "value": 244.86719240461076,
            "unit": "ns",
            "range": "± 0.18662890312512428"
          },
          {
            "name": "SpanBitsBenchmark.Set_BitArray(BitCount: 65536)",
            "value": 15844.105825570914,
            "unit": "ns",
            "range": "± 10.212798558973217"
          },
          {
            "name": "SpanBitsBenchmark.Set_SpanBits(BitCount: 65536)",
            "value": 15491.033232625325,
            "unit": "ns",
            "range": "± 61.60059028309006"
          },
          {
            "name": "CacheLocalityBenchmark.Dictionary_Shuffled(ItemCount: 1000000)",
            "value": 11198460.306783536,
            "unit": "ns",
            "range": "± 398524.56098680815"
          },
          {
            "name": "CacheLocalityBenchmark.IntDictionary_Shuffled(ItemCount: 1000000)",
            "value": 3268973.3515625,
            "unit": "ns",
            "range": "± 7019.20497824722"
          },
          {
            "name": "BitPackingBenchmark.Unpack_BclBitArray",
            "value": 314535.6493094308,
            "unit": "ns",
            "range": "± 104.20397427771006"
          },
          {
            "name": "BitPackingBenchmark.Unpack_BitReader",
            "value": 21215.324432373047,
            "unit": "ns",
            "range": "± 13.676465036116513"
          },
          {
            "name": "BranchlessBenchmark.Ternary_Unpredictable(Length: 1000000)",
            "value": 4621845.967548077,
            "unit": "ns",
            "range": "± 1332.6243507209151"
          },
          {
            "name": "BranchlessBenchmark.Branchless_Unpredictable(Length: 1000000)",
            "value": 941321.0072115385,
            "unit": "ns",
            "range": "± 560.5114225769354"
          },
          {
            "name": "GuidBenchmark.V4_BclNewGuid",
            "value": 2722619.1583533655,
            "unit": "ns",
            "range": "± 5567.540064225559"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_Xoshiro",
            "value": 73314.82676595052,
            "unit": "ns",
            "range": "± 34.29958806086283"
          },
          {
            "name": "GuidBenchmark.V4_FastGuid_WyRand",
            "value": 72414.77801106771,
            "unit": "ns",
            "range": "± 21.238555524594872"
          },
          {
            "name": "GuidBenchmark.V7_BclNewGuid",
            "value": 2718823.048828125,
            "unit": "ns",
            "range": "± 1141.428548210239"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Stateless",
            "value": 68448.77673339844,
            "unit": "ns",
            "range": "± 30.048217556859075"
          },
          {
            "name": "GuidBenchmark.V7_FastGuid_Monotonic",
            "value": 63760.77879450871,
            "unit": "ns",
            "range": "± 17.007837206114687"
          },
          {
            "name": "RealWorldWorkloadBenchmark.Dictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 14015848.696969697,
            "unit": "ns",
            "range": "± 439592.9546858153"
          },
          {
            "name": "RealWorldWorkloadBenchmark.IntDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 8123615.333333333,
            "unit": "ns",
            "range": "± 138758.02687292243"
          },
          {
            "name": "RealWorldWorkloadBenchmark.CelerityDictionary_Workload(ItemCount: 100000, OpCount: 500000)",
            "value": 5841520.829787234,
            "unit": "ns",
            "range": "± 207934.39562278742"
          }
        ]
      }
    ]
  }
}