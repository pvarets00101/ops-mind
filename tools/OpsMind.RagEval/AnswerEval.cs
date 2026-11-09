using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpsMind.Core.Evaluation;
using OpsMind.Core.Rag;

/// <summary>
/// Оценка ответов RAG (режим --answers): поиск → ответ модели (RagAnswerer, как в /api/ask) → судья.
///  - вопросы из базы: отвечено ли, сослался ли на нужный регламент, вердикт судьи по эталонному ответу и найденным фрагментам;
///  - вопросы вне базы: отказ по порогу поиска, отказ модели или пропуск (модель ответила на вопрос, которого нет в регламентах);
///  - токены и время на ответ.
/// </summary>
static class AnswerEval {
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static async Task<string> RunAsync(
        RagAnswerer rag, AnswerJudge? judge, List<EvalQuestion> questions, List<OffTopicQuestion> offTopic,
        string label, string modelName, string judgeName) {
        var answered = new List<AnswerOutcome>();
        foreach (var q in questions) {
            Console.Write($"{q.Id} {q.Question} … ");
            var sw = Stopwatch.StartNew();
            RagAnswer reply;
            try {
                reply = await rag.AskAsync(q.Question);
            } catch (Exception e) {
                Console.WriteLine($"ошибка: {e.Message}");
                answered.Add(new AnswerOutcome(q, null, 0, null, $"ОШИБКА: {e.Message}"));
                continue;
            }
            var seconds = sw.Elapsed.TotalSeconds;

            JudgeVerdict? verdict = null;
            string? judgeError = null;
            if (judge is not null && reply.Grounded) {
                try {
                    verdict = await judge.EvaluateAsync(new JudgeCase(
                        [q.Question],
                        RagAnswerer.FormatContext(reply.Sources),
                        reply.Answer,
                        $"Эталонный ответ: {q.ReferenceAnswer} Ответ должен опираться на регламент {q.ExpectedRunbook}."));
                } catch (Exception e) {
                    judgeError = e.Message;
                }
            }

            var outcome = new AnswerOutcome(q, reply, seconds, verdict, judgeError);
            answered.Add(outcome);
            Console.WriteLine(
                $"{(!reply.Grounded ? "ОТКАЗ" : outcome.CitesExpected ? "ссылка OK" : "ссылка ✗")}" +
                $"{(verdict is null ? "" : verdict.Pass ? ", судья OK" : $", судья ✗ ({verdict.Failed})")}" +
                $"{(judgeError is null ? "" : $", судья: ошибка {judgeError}")}, {seconds:N1} с");
        }

        var refusals = new List<(OffTopicQuestion Q, RagAnswer? Reply, double Seconds)>();
        foreach (var q in offTopic) {
            Console.Write($"{q.Id} {q.Question} … ");
            var sw = Stopwatch.StartNew();
            RagAnswer? reply = null;
            try {
                reply = await rag.AskAsync(q.Question);
            } catch (Exception e) {
                Console.Write($"ошибка: {e.Message} ");
            }
            refusals.Add((q, reply, sw.Elapsed.TotalSeconds));
            Console.WriteLine(reply is null ? "" : !reply.ModelCalled ? "отказ по порогу" : !reply.Grounded ? "отказ модели" : "ПРОПУСК");
        }

        return BuildReport(answered, refusals, label, modelName, judgeName, judge is not null);
    }

    static string BuildReport(
        List<AnswerOutcome> answered, List<(OffTopicQuestion Q, RagAnswer? Reply, double Seconds)> refusals,
        string label, string modelName, string judgeName, bool withJudge) {
        var ok = answered.Where(a => a.Reply is not null).ToList();
        var replied = ok.Where(a => a.Reply!.Grounded).ToList();
        var judged = replied.Where(a => a.Verdict is not null).ToList();
        double Share<T>(List<T> list, Func<T, bool> f) => list.Count == 0 ? 0 : list.Count(f) / (double)list.Count;

        var report = new StringBuilder()
            .AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} — {label}")
            .AppendLine()
            .AppendLine($"Модель ответа: {modelName}. Судья: {judgeName}. Вопросов из базы: {answered.Count}, вне базы: {refusals.Count}.")
            .AppendLine();

        // Главная строка: верный ответ = модель ответила (не отказала) и судья принял ответ по всем критериям.
        report
            .AppendLine("| Верный ответ | Отвечено | Ссылка на нужный RB | Судья: верно | Подтверждено | Полно | Отказ вне базы | Время (поиск + ответ) |")
            .AppendLine("|---|---|---|---|---|---|---|---|")
            .AppendLine(string.Create(Inv,
                $"| {(withJudge ? $"{Share(answered, a => a.Reply?.Grounded == true && a.Verdict?.Pass == true):P0}" : "—")} " +
                $"| {Share(answered, a => a.Reply?.Grounded == true):P0} | {Share(replied, a => a.CitesExpected):P0} " +
                $"| {(withJudge ? $"{Share(judged, a => a.Verdict!.Correct):P0}" : "—")} " +
                $"| {(withJudge ? $"{Share(judged, a => a.Verdict!.Grounded):P0}" : "—")} " +
                $"| {(withJudge ? $"{Share(judged, a => a.Verdict!.Complete):P0}" : "—")} " +
                $"| {Share(refusals, r => r.Reply is { Grounded: false }):P0} " +
                $"| {(replied.Count == 0 ? 0 : replied.Average(a => a.Seconds)):N1} с |"))
            .AppendLine();

        // Токены — только вызовы модели ответа (без судьи). Для claude -p вход включает служебный промпт CLI,
        // часть входа читается из кэша промптов.
        var calls = ok.Select(a => a.Reply!).Concat(refusals.Where(r => r.Reply is not null).Select(r => r.Reply!))
            .Where(r => r.ModelCalled && r.Usage is not null).ToList();
        if (calls.Count > 0) {
            var input = calls.Average(r => (double)(r.Usage!.InputTokenCount ?? 0));
            var cached = calls.Average(r => (double)(r.Usage!.CachedInputTokenCount ?? 0));
            var output = calls.Average(r => (double)(r.Usage!.OutputTokenCount ?? 0));
            report.AppendLine(string.Create(Inv,
                    $"Токены на вызов модели (вызовов: {calls.Count}): вход {input:N0} (из кэша {cached:N0}), выход {output:N0}. " +
                    $"Всего за прогон: вход {calls.Sum(r => r.Usage!.InputTokenCount ?? 0):N0}, выход {calls.Sum(r => r.Usage!.OutputTokenCount ?? 0):N0}."))
                .AppendLine();
        }

        var byThreshold = refusals.Count(r => r.Reply is { ModelCalled: false });
        var byModel = refusals.Count(r => r.Reply is { ModelCalled: true, Grounded: false });
        report.AppendLine($"Вопросы вне базы: отказ по порогу поиска — {byThreshold}, отказ модели — {byModel}, " +
                          $"ответ вместо отказа — {refusals.Count(r => r.Reply is { Grounded: true })}, ошибок — {refusals.Count(r => r.Reply is null)}.")
            .AppendLine();

        var falseRefusals = ok.Where(a => !a.Reply!.Grounded).ToList();
        if (falseRefusals.Count > 0) {
            report.AppendLine("Отказы на вопросы из базы:").AppendLine();
            foreach (var a in falseRefusals) {
                var how = a.Reply!.ModelCalled ? "модель" : "порог поиска";
                report.AppendLine($"- **{a.Question.Id}** ({how}): {a.Question.Question} → {Short(a.Reply.Answer, 200)}");
            }
            report.AppendLine();
        }

        var leaks = refusals.Where(r => r.Reply is { Grounded: true }).ToList();
        if (leaks.Count > 0) {
            report.AppendLine("Ответы на вопросы вне базы (должен быть отказ):").AppendLine();
            foreach (var (q, reply, _) in leaks)
                report.AppendLine($"- **{q.Id}** ({q.Kind}): {q.Question} → {Short(reply!.Answer, 300)}");
            report.AppendLine();
        }

        var rejected = judged.Where(a => !a.Verdict!.Pass).ToList();
        if (rejected.Count > 0) {
            report.AppendLine("Замечания судьи:").AppendLine();
            foreach (var a in rejected) {
                report.AppendLine($"- **{a.Question.Id}** ({a.Verdict!.Failed}): {a.Question.Question}")
                    .AppendLine($"  - Ответ: {Short(a.Reply!.Answer, 400)}")
                    .AppendLine($"  - Эталон: {a.Question.ReferenceAnswer}")
                    .AppendLine($"  - Судья: {Short(a.Verdict.Reason, 500)}");
            }
            report.AppendLine();
        }

        var wrongCitation = replied.Where(a => !a.CitesExpected && a.Verdict?.Pass != false).ToList();
        if (wrongCitation.Count > 0) {
            report.AppendLine("Нет ссылки на ожидаемый регламент (судья ответ принял — возможно, ответ есть и в другом RB):").AppendLine();
            foreach (var a in wrongCitation)
                report.AppendLine($"- **{a.Question.Id}** (ожидался {a.Question.ExpectedRunbook}): {Short(a.Reply!.Answer, 200)}");
            report.AppendLine();
        }

        var errors = answered.Where(a => a.Reply is null || a.JudgeError is not null).ToList();
        if (errors.Count > 0) {
            report.AppendLine("Ошибки:").AppendLine();
            foreach (var a in errors)
                report.AppendLine($"- **{a.Question.Id}**: {a.JudgeError ?? "ошибка ответа"}");
            report.AppendLine();
        }
        return report.ToString();
    }

    static string Short(string s, int max) {
        s = s.ReplaceLineEndings(" ").Replace("|", "\\|");
        return s.Length <= max ? s : s[..max] + "…";
    }
}

record AnswerOutcome(EvalQuestion Question, RagAnswer? Reply, double Seconds, JudgeVerdict? Verdict, string? JudgeError) {
    public bool CitesExpected => Reply?.Answer.Contains(Question.ExpectedRunbook, StringComparison.OrdinalIgnoreCase) == true;
}
