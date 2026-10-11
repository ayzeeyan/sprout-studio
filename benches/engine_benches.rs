use criterion::{BenchmarkId, Criterion, Throughput, black_box, criterion_group, criterion_main};
use sprout_core::execute;
use sprout_core::lexer::lex;
use sprout_core::parser::{SyntaxProfile, parse, parse_profile};
use sprout_core::source::SourceFile;

const SAMPLE_SMALL: &str = include_str!("samples/small.spr");
const SAMPLE_MEDIUM: &str = include_str!("samples/medium.spr");
const SAMPLE_LARGE: &str = include_str!("samples/large.spr");
const SAMPLE_CPU_LOOPS: &str = include_str!("samples/cpu_loops.spr");
const SAMPLE_CPU_RECURSION: &str = include_str!("samples/cpu_recursion.spr");
const SAMPLE_CPU_STRINGS: &str = include_str!("samples/cpu_strings.spr");

fn bench_lexing(c: &mut Criterion) {
    let mut group = c.benchmark_group("lexing_throughput");

    let samples = [
        ("small", SAMPLE_SMALL),
        ("medium", SAMPLE_MEDIUM),
        ("large", SAMPLE_LARGE),
        ("cpu_loops", SAMPLE_CPU_LOOPS),
        ("cpu_recursion", SAMPLE_CPU_RECURSION),
        ("cpu_strings", SAMPLE_CPU_STRINGS),
    ];

    for (name, content) in samples {
        let source = SourceFile::new(format!("{name}.spr"), content);
        group.throughput(Throughput::Bytes(content.len() as u64));
        group.bench_with_input(BenchmarkId::from_parameter(name), &source, |b, src| {
            b.iter(|| {
                let res = lex(black_box(src));
                black_box(res);
            });
        });
    }

    group.finish();
}

fn bench_parsing(c: &mut Criterion) {
    let mut group = c.benchmark_group("parsing_latency");

    let samples = [
        ("small", SAMPLE_SMALL),
        ("medium", SAMPLE_MEDIUM),
        ("large", SAMPLE_LARGE),
    ];

    for (name, content) in samples {
        let source = SourceFile::new(format!("{name}.spr"), content);
        group.throughput(Throughput::Bytes(content.len() as u64));
        group.bench_with_input(BenchmarkId::new("guided", name), &source, |b, src| {
            b.iter(|| {
                let res = parse(black_box(src));
                black_box(res);
            });
        });
    }

    // Benchmark different syntax profiles
    let guided_text =
        "has a = 10\nhas b = 20\naction sum(x, y)\n    return x + y\nend\nprint(sum(a, b))\n";
    let structured_text = "int a = 10;\nint b = 20;\nint sum(int x, int y) {\n    return x + y;\n}\nprint(sum(a, b));\n";
    let advanced_text = "a := 10\nb := 20\nsum(x, y) =>\n    x + y\nprint(sum(a, b))\n";

    let guided_src = SourceFile::new("guided_profile.spr", guided_text);
    let structured_src = SourceFile::new("structured_profile.spr", structured_text);
    let advanced_src = SourceFile::new("advanced_profile.spr", advanced_text);

    group.bench_function("profile_guided", |b| {
        b.iter(|| {
            let res = parse_profile(black_box(&guided_src), SyntaxProfile::Guided);
            black_box(res);
        });
    });

    group.bench_function("profile_structured", |b| {
        b.iter(|| {
            let res = parse_profile(black_box(&structured_src), SyntaxProfile::Structured);
            black_box(res);
        });
    });

    group.bench_function("profile_advanced", |b| {
        b.iter(|| {
            let res = parse_profile(black_box(&advanced_src), SyntaxProfile::Advanced);
            black_box(res);
        });
    });

    group.finish();
}

fn bench_runtime_execution(c: &mut Criterion) {
    let mut group = c.benchmark_group("runtime_execution");

    let workloads = [
        ("small_loop", SAMPLE_SMALL),
        ("medium_classes", SAMPLE_MEDIUM),
        ("large_program", SAMPLE_LARGE),
        ("cpu_loops", SAMPLE_CPU_LOOPS),
        ("cpu_recursion", SAMPLE_CPU_RECURSION),
        ("cpu_strings", SAMPLE_CPU_STRINGS),
    ];

    for (name, content) in workloads {
        let source = SourceFile::new(format!("{name}.spr"), content);
        group.bench_with_input(BenchmarkId::from_parameter(name), &source, |b, src| {
            b.iter(|| {
                let res = execute(black_box(src));
                black_box(res);
            });
        });
    }

    group.finish();
}

criterion_group!(
    benches,
    bench_lexing,
    bench_parsing,
    bench_runtime_execution
);
criterion_main!(benches);
