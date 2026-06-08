window.BENCHMARK_DATA = {
  "lastUpdate": 1780909140506,
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
      }
    ]
  }
}