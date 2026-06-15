window.BENCHMARK_DATA = {
  "lastUpdate": 1781515486056,
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
      }
    ]
  }
}