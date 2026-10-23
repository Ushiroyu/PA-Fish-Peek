# 中文检索评测记录

数据集：`retrieval-scenarios-1`；资料版本：`knowledge-2026-10-23.2`。

根据现有实现和随附文档设计的离线场景问句；非线上用户统计。相关性标签附逐条依据，供维护者复核。

策略：`hex-normalized`，字符与双字词 BM25；未调用模型。

| 分组 | 有依据问题 | 无答案问题 | Recall@4 | MRR@4 |
| --- | ---: | ---: | ---: | ---: |
| all | 50 | 10 | 0.9600 | 0.8517 |
| abbreviation | 10 | 0 | 1.0000 | 0.9333 |
| direct | 10 | 0 | 1.0000 | 0.9500 |
| missing_context | 10 | 0 | 0.9500 | 0.7167 |
| multi_evidence | 10 | 0 | 0.9500 | 0.9500 |
| no_answer | 0 | 10 | — | — |
| paraphrase | 10 | 0 | 0.9000 | 0.7083 |

无答案样例的空检索率：0.0%。该值仅观察是否返回文段，不代表模型拒答准确率。

## 需复核样例

返回边界说明可能有助于澄清问题；无答案样例返回文段时单独列出，不直接判为模型错误。

### D05 · direct

F10和F11有什么作用？

标注依据：说明输出开关和范围切换。
预期段落：operation#安装与按键
实际前四：tools#当前状态与条件澄清；operation#安装与按键；operation#遭遇采集；operation#临时玩家标签
复核标记：first_result_not_labelled_relevant

### P01 · paraphrase

为什么天晴了流星鱼还在名单里？

标注依据：天气主要参与评分，不是通用硬过滤。
预期段落：fishing#环境评分与概率
实际前四：fishing#流星鱼与奇虾；fishing#环境评分与概率；tools#当前状态与条件澄清；fishing#地点与天气标记
复核标记：first_result_not_labelled_relevant

### P04 · paraphrase

问了好几遍完全一样的工具会不会一直转圈？

标注依据：相同工具参数重复第三次时停止。
预期段落：reliability#调用预算
实际前四：fishing#未知条件；operation#安装与按键；operation#遭遇采集；reliability#调用预算
复核标记：first_result_not_labelled_relevant

### P05 · paraphrase

服务偶尔限流了会自动再试吗？

标注依据：429等临时错误重试一次。
预期段落：reliability#重试与错误恢复
实际前四：fishing#环境评分与概率；operation#遭遇采集；versions#显式查询与插件回退；tools#当前状态与条件澄清
复核标记：no_relevant_section_at_4；first_result_not_labelled_relevant

### P07 · paraphrase

没有遇到鱼的时候页面会不会自己编几条？

标注依据：两段共同说明真实事件不由示例补充。
预期段落：operation#遭遇采集；versions#事件写入与轮转
实际前四：tools#当前状态与条件澄清；operation#安装与按键；operation#遭遇采集；versions#事件写入与轮转
复核标记：first_result_not_labelled_relevant

### A09 · abbreviation

c0000005在哪个模块出过？

标注依据：记录模块及历史偏移。
预期段落：operation#稳定性诊断
实际前四：reliability#调用预算；operation#临时玩家标签；operation#稳定性诊断；tools#桌面工具与网页问答
复核标记：first_result_not_labelled_relevant

### M02 · multi_evidence

怎么打开提示，进入后闪退又该怎样排查？

标注依据：需要操作和诊断两段。
预期段落：operation#安装与按键；operation#稳定性诊断
实际前四：operation#安装与按键；versions#事件写入与轮转；operation#遭遇采集；reliability#Trace 与记录范围
复核标记：incomplete_evidence_at_4

### M06 · multi_evidence

模型反复调用和接口503分别怎么处理？

标注依据：需要循环预算和请求重试策略。
预期段落：reliability#调用预算；reliability#重试与错误恢复
实际前四：tools#桌面工具与网页问答；reliability#调用预算；reliability#重试与错误恢复；operation#遭遇采集
复核标记：first_result_not_labelled_relevant

### C02 · missing_context

不知道天气时应该怎么查候选？

标注依据：未知天气不增加天气匹配分。
预期段落：fishing#未知条件
实际前四：tools#查询与解释的分工；fishing#流星鱼与奇虾；fishing#未知条件；tools#当前状态与条件澄清
复核标记：first_result_not_labelled_relevant

### C05 · missing_context

没认出鱼竿能直接随便换一根继续算吗？

标注依据：未知鱼竿报错，工作台不自动更换。
预期段落：fishing#未知条件；versions#显式查询与插件回退
实际前四：operation#安装与按键；reliability#重试与错误恢复；fishing#未知条件；fishing#候选鱼种
复核标记：incomplete_evidence_at_4；first_result_not_labelled_relevant

### C07 · missing_context

time=0能说明现在是白天吗？

标注依据：0为未知且不能从输入推断现场。
预期段落：fishing#地点与天气标记；tools#当前状态与条件澄清
实际前四：tools#查询与解释的分工；tools#当前状态与条件澄清；fishing#流星鱼与奇虾；fishing#地点与天气标记
复核标记：first_result_not_labelled_relevant

### C09 · missing_context

工具不认识我输入的鱼名怎么办？

标注依据：未知鱼名返回参数错误。
预期段落：fishing#未知条件
实际前四：tools#当前状态与条件澄清；fishing#未知条件；tools#查询与解释的分工；tools#检索与事件参数范围
复核标记：first_result_not_labelled_relevant

### C10 · missing_context

有匿名遭遇是不是就知道真实鱼种了？

标注依据：事件不包含真实鱼种结论。
预期段落：tools#当前状态与条件澄清
实际前四：operation#遭遇采集；tools#当前状态与条件澄清；fishing#环境评分与概率；versions#事件写入与轮转
复核标记：first_result_not_labelled_relevant

### N01 · no_answer

下个月将会新增哪种鱼？

标注依据：资料没有未来更新公告。
预期段落：无直接答案
实际前四：versions#数据快照一致性；fishing#未知条件；tools#检索与事件参数范围；tools#当前状态与条件澄清
复核标记：no_answer_context_needs_review

### N02 · no_answer

帮我查玩家真实手机号和账号密码。

标注依据：资料与事件均没有个人凭据。
预期段落：无直接答案
实际前四：operation#临时玩家标签；operation#遭遇采集；tools#当前状态与条件澄清；operation#安装与按键
复核标记：no_answer_context_needs_review

### N03 · no_answer

今天服务器的流星鱼真实掉落概率是多少？

标注依据：环境分数不提供服务器真实概率。
预期段落：无直接答案
实际前四：fishing#环境评分与概率；fishing#流星鱼与奇虾；tools#当前状态与条件澄清；fishing#地点与天气标记
复核标记：no_answer_context_needs_review

### N04 · no_answer

北京明天会不会下雨？

标注依据：游戏天气说明不包含现实天气预报。
预期段落：无直接答案
实际前四：fishing#未知条件；operation#遭遇采集；fishing#地点与天气标记；fishing#流星鱼与奇虾
复核标记：no_answer_context_needs_review

### N05 · no_answer

这家公司的年终奖金是多少？

标注依据：资料不涉及薪酬。
预期段落：无直接答案
实际前四：reliability#调用预算；operation#临时玩家标签；versions#显式查询与插件回退；operation#安装与按键
复核标记：no_answer_context_needs_review

### N06 · no_answer

我的信用卡账单该还多少钱？

标注依据：资料没有金融账户信息。
预期段落：无直接答案
实际前四：operation#临时玩家标签；reliability#调用预算；tools#当前状态与条件澄清；reliability#引用与可信依据
复核标记：no_answer_context_needs_review

### N07 · no_answer

主机显卡现在的温度是多少？

标注依据：资料不含实时硬件传感器值。
预期段落：无直接答案
实际前四：tools#当前状态与条件澄清；operation#安装与按键；reliability#调用预算；operation#稳定性诊断
复核标记：no_answer_context_needs_review

### N08 · no_answer

现在有多少玩家在线？

标注依据：事件通道不提供服务器在线人数。
预期段落：无直接答案
实际前四：operation#临时玩家标签；operation#遭遇采集；operation#安装与按键；tools#当前状态与条件澄清
复核标记：no_answer_context_needs_review

### N09 · no_answer

安装这个插件一定不会封号吗？

标注依据：资料没有封号风险保证。
预期段落：无直接答案
实际前四：operation#安装与按键；operation#遭遇采集；versions#显式查询与插件回退；fishing#未知条件
复核标记：no_answer_context_needs_review

### N10 · no_answer

这根鱼竿目前在商城卖多少钱？

标注依据：鱼竿档案不包含实时价格。
预期段落：无直接答案
实际前四：fishing#未知条件；reliability#调用预算；tools#当前状态与条件澄清；operation#安装与按键
复核标记：no_answer_context_needs_review

