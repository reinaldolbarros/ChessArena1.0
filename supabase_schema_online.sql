-- ============================================================
-- ChessMAUI — Modo Online real (partidas, matchmaking, Elo protegido)
-- Execute no SQL Editor do painel, DEPOIS do supabase_schema.sql já aplicado.
-- ============================================================

-- ── Tabela: games (partida real entre dois jogadores) ────────
create table if not exists public.games (
  id                  uuid        primary key default gen_random_uuid(),
  white_id            uuid        not null references auth.users,
  black_id            uuid        not null references auth.users,
  time_minutes        int         not null,
  moves               text[]      not null default '{}',   -- lances em UCI, ex. "e2e4"
  turn                text        not null default 'white' check (turn in ('white','black')),
  white_remaining_ms  bigint      not null,
  black_remaining_ms  bigint      not null,
  last_move_at        timestamptz not null default now(),
  status              text        not null default 'active' check (status in ('active','finished','aborted')),
  result              text        check (result in ('white','black','draw')),
  end_reason          text,       -- 'checkmate_or_rule' | 'resign' | 'timeout' | 'draw_agreed'
  draw_offered_by     uuid,
  white_claim         text check (white_claim in ('white','black','draw')),
  black_claim         text check (black_claim in ('white','black','draw')),
  created_at          timestamptz not null default now()
);

create index if not exists games_participants_idx on public.games (white_id, black_id);

alter table public.games enable row level security;

create policy "Participantes leem a própria partida"
  on public.games for select
  using (auth.uid() in (white_id, black_id));

-- Só o próprio ato de anexar um lance passa por UPDATE direto do cliente — validado pelo
-- gatilho validate_game_move logo abaixo. Toda transição de fim de partida (xeque-mate/
-- empate/desistência/timeout) passa pelas funções SECURITY DEFINER mais abaixo, nunca por
-- um UPDATE livre do cliente.
create policy "Participantes atualizam a própria partida"
  on public.games for update
  using (auth.uid() in (white_id, black_id))
  with check (auth.uid() in (white_id, black_id));

-- Sem policy de INSERT/DELETE para authenticated/anon: a única forma de criar uma linha em
-- games é através de find_match()/accept_challenge(), que rodam como SECURITY DEFINER (dono
-- "postgres", que ignora RLS) — assim nenhum cliente cria partidas "do nada".


-- ── Tabela: matchmaking_queue (fila de busca de partida) ─────
create table if not exists public.matchmaking_queue (
  user_id      uuid        primary key references auth.users on delete cascade,
  rating       int         not null,
  time_minutes int         not null,
  created_at   timestamptz not null default now()
);

alter table public.matchmaking_queue enable row level security;
-- Nenhuma policy: só as funções SECURITY DEFINER abaixo tocam essa tabela.


-- ── Trava de lances: só 1 lance novo, do dono do turno, relógio recalculado no servidor ──
create or replace function public.validate_game_move()
returns trigger language plpgsql as $$
declare
  mover     uuid;
  old_len   int := coalesce(array_length(old.moves, 1), 0);
  new_len   int := coalesce(array_length(new.moves, 1), 0);
  elapsed_ms bigint;
begin
  -- Transições de fim de partida/oferta de empate/etc. são feitas pelas próprias funções
  -- SECURITY DEFINER abaixo, que ligam essa flag pra pular a checagem "1 lance por vez".
  if coalesce(current_setting('chessmaui.trusted_game_write', true), '') = 'true' then
    return new;
  end if;

  if old.status <> 'active' then
    raise exception 'Esta partida não está mais ativa.';
  end if;

  if new_len <> old_len + 1 then
    raise exception 'Só é permitido enviar exatamente 1 lance novo por vez.';
  end if;

  if new.moves[1:old_len] is distinct from old.moves then
    raise exception 'O histórico de lances não pode ser alterado.';
  end if;

  mover := case old.turn when 'white' then old.white_id else old.black_id end;
  if auth.uid() <> mover then
    raise exception 'Não é o seu turno.';
  end if;

  -- Recalcula os relógios a partir do tempo real decorrido no servidor — ignora qualquer
  -- valor de relógio que o próprio cliente tenha mandado no UPDATE.
  elapsed_ms := greatest(0, floor(extract(epoch from (now() - old.last_move_at)) * 1000))::bigint;

  if old.turn = 'white' then
    new.white_remaining_ms := greatest(0, old.white_remaining_ms - elapsed_ms);
    new.black_remaining_ms := old.black_remaining_ms;
    new.turn := 'black';
  else
    new.black_remaining_ms := greatest(0, old.black_remaining_ms - elapsed_ms);
    new.white_remaining_ms := old.white_remaining_ms;
    new.turn := 'white';
  end if;

  new.last_move_at    := now();
  new.white_claim      := null;
  new.black_claim      := null;
  new.draw_offered_by  := null;

  return new;
end;
$$;

drop trigger if exists validate_game_move_trigger on public.games;
create trigger validate_game_move_trigger
  before update on public.games
  for each row execute procedure public.validate_game_move();


-- ── Trava de rating: elo/vitórias/derrotas só mudam por função confiável do servidor ──
-- Mesmo molde do protect_is_admin_trigger que já existe: mas aqui usamos uma flag de
-- transação em vez de checar auth.role(), porque as funções abaixo (SECURITY DEFINER)
-- ainda "veem" auth.role() = 'authenticated' mesmo rodando como servidor — só essa flag,
-- ligada só de dentro das próprias funções de confiança, consegue distinguir os dois casos.
create or replace function public.protect_profile_ratings()
returns trigger language plpgsql as $$
begin
  if coalesce(current_setting('chessmaui.trusted_rating_write', true), '') <> 'true' then
    new.elo             := old.elo;
    new.week_elo        := old.week_elo;
    new.wins            := old.wins;
    new.losses          := old.losses;
    new.tournaments_won := old.tournaments_won;
  end if;
  return new;
end;
$$;

drop trigger if exists protect_profile_ratings_trigger on public.profiles;
create trigger protect_profile_ratings_trigger
  before update on public.profiles
  for each row execute procedure public.protect_profile_ratings();


-- ── Elo: mesma fórmula de ProfileService.UpdateElo, agora só existe aqui ──
create or replace function public.compute_elo_delta(
  p_current_elo int, p_opponent_elo int, p_games_played int, p_score numeric
) returns int language plpgsql immutable as $$
declare
  k          int := case when p_games_played < 30 then 40 else 20 end;
  expected   numeric := 1.0 / (1.0 + power(10.0, (p_opponent_elo - p_current_elo)::numeric / 400.0));
  raw_delta  int := round(k * (p_score - expected));
  new_elo    int := greatest(100, p_current_elo + raw_delta);
begin
  return new_elo - p_current_elo;
end;
$$;


-- ── Finalização de partida online: só é chamada internamente pelas funções abaixo,
--    nunca diretamente pelo cliente (revoke no fim do arquivo). ──
create or replace function public.finalize_game(p_game_id uuid, p_result text, p_end_reason text)
returns void language plpgsql security definer as $$
declare
  g record;
  white_games int; black_games int;
  white_score numeric; black_score numeric;
  white_delta int; black_delta int;
  white_elo int; black_elo int;
begin
  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;

  perform set_config('chessmaui.trusted_game_write', 'true', true);
  update public.games
     set status = 'finished', result = p_result, end_reason = p_end_reason
   where id = p_game_id;

  select elo, wins + losses into white_elo, white_games from public.profiles where id = g.white_id;
  select elo, wins + losses into black_elo, black_games from public.profiles where id = g.black_id;

  white_score := case p_result when 'white' then 1.0 when 'draw' then 0.5 else 0.0 end;
  black_score := 1.0 - white_score;

  white_delta := public.compute_elo_delta(coalesce(white_elo,1200), coalesce(black_elo,1200), coalesce(white_games,0), white_score);
  black_delta := public.compute_elo_delta(coalesce(black_elo,1200), coalesce(white_elo,1200), coalesce(black_games,0), black_score);

  perform set_config('chessmaui.trusted_rating_write', 'true', true);
  update public.profiles set
    elo      = elo + white_delta,
    week_elo = week_elo + white_delta,
    wins     = wins   + case when p_result = 'white' then 1 else 0 end,
    losses   = losses + case when p_result = 'black' then 1 else 0 end
  where id = g.white_id;

  perform set_config('chessmaui.trusted_rating_write', 'true', true);
  update public.profiles set
    elo      = elo + black_delta,
    week_elo = week_elo + black_delta,
    wins     = wins   + case when p_result = 'black' then 1 else 0 end,
    losses   = losses + case when p_result = 'white' then 1 else 0 end
  where id = g.black_id;
end;
$$;


-- ── Matchmaking real ──────────────────────────────────────────
create or replace function public.find_match(p_time_minutes int)
returns uuid language plpgsql security definer as $$
declare
  me          uuid := auth.uid();
  my_elo      int;
  opp         record;
  new_game_id uuid;
  coin        boolean;
  white_id_v  uuid;
  black_id_v  uuid;
  margin      int;
begin
  if me is null then raise exception 'Não autenticado.'; end if;

  select elo into my_elo from public.profiles where id = me;
  if my_elo is null then my_elo := 1200; end if;

  delete from public.matchmaking_queue where created_at < now() - interval '2 minutes';

  insert into public.matchmaking_queue (user_id, rating, time_minutes, created_at)
  values (me, my_elo, p_time_minutes, now())
  on conflict (user_id) do update
    set rating = excluded.rating, time_minutes = excluded.time_minutes;

  select q.* into opp
  from public.matchmaking_queue q
  where q.user_id <> me
    and q.time_minutes = p_time_minutes
    and abs(q.rating - my_elo) <= greatest(100, least(400, 100 + 60 * (extract(epoch from (now() - q.created_at))::int / 5)))
  order by q.created_at asc
  for update skip locked
  limit 1;

  if opp.user_id is null then
    return null;
  end if;

  delete from public.matchmaking_queue where user_id in (me, opp.user_id);

  coin       := random() < 0.5;
  white_id_v := case when coin then me else opp.user_id end;
  black_id_v := case when coin then opp.user_id else me end;
  new_game_id := gen_random_uuid();

  perform set_config('chessmaui.trusted_game_write', 'true', true);
  insert into public.games (
    id, white_id, black_id, time_minutes, moves, turn,
    white_remaining_ms, black_remaining_ms, last_move_at, status
  ) values (
    new_game_id, white_id_v, black_id_v, p_time_minutes, '{}', 'white',
    p_time_minutes::bigint * 60000, p_time_minutes::bigint * 60000, now(), 'active'
  );

  return new_game_id;
end;
$$;

create or replace function public.cancel_search()
returns void language plpgsql security definer as $$
begin
  delete from public.matchmaking_queue where user_id = auth.uid();
end;
$$;


-- ── Reivindicação de resultado por consenso (xeque-mate/afogamento/empate por regra) ──
-- Só finaliza e libera Elo quando os DOIS lados reivindicam o mesmo resultado — um cliente
-- sozinho não consegue forjar uma vitória.
create or replace function public.submit_result_claim(p_game_id uuid, p_claim text)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  g  record;
begin
  if p_claim not in ('white','black','draw') then raise exception 'Resultado inválido.'; end if;

  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;
  if me <> g.white_id and me <> g.black_id then raise exception 'Você não participa dessa partida.'; end if;

  perform set_config('chessmaui.trusted_game_write', 'true', true);
  if me = g.white_id then
    update public.games set white_claim = p_claim where id = p_game_id;
  else
    update public.games set black_claim = p_claim where id = p_game_id;
  end if;

  select * into g from public.games where id = p_game_id;
  if g.white_claim is not null and g.white_claim = g.black_claim then
    perform public.finalize_game(p_game_id, g.white_claim, 'checkmate_or_rule');
  end if;
end;
$$;

create or replace function public.resign_game(p_game_id uuid)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  g  record;
  winner text;
begin
  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;
  if me <> g.white_id and me <> g.black_id then raise exception 'Você não participa dessa partida.'; end if;

  winner := case when me = g.white_id then 'black' else 'white' end;
  perform public.finalize_game(p_game_id, winner, 'resign');
end;
$$;

-- Verifica no PRÓPRIO servidor (não confia no relógio do cliente) se o tempo do lado que
-- devia jogar realmente estourou, antes de dar a vitória por tempo.
create or replace function public.claim_timeout(p_game_id uuid)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  g  record;
  mover_remaining bigint;
  elapsed_ms bigint;
  winner text;
begin
  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;
  if me <> g.white_id and me <> g.black_id then raise exception 'Você não participa dessa partida.'; end if;

  mover_remaining := case g.turn when 'white' then g.white_remaining_ms else g.black_remaining_ms end;
  elapsed_ms := greatest(0, floor(extract(epoch from (now() - g.last_move_at)) * 1000))::bigint;

  if elapsed_ms <= mover_remaining then
    raise exception 'O adversário ainda não estourou o tempo.';
  end if;

  winner := case g.turn when 'white' then 'black' else 'white' end;
  perform public.finalize_game(p_game_id, winner, 'timeout');
end;
$$;

create or replace function public.offer_draw(p_game_id uuid)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  g  record;
begin
  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;
  if me <> g.white_id and me <> g.black_id then raise exception 'Você não participa dessa partida.'; end if;

  perform set_config('chessmaui.trusted_game_write', 'true', true);
  update public.games set draw_offered_by = me where id = p_game_id;
end;
$$;

create or replace function public.respond_draw(p_game_id uuid, p_accept boolean)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  g  record;
begin
  select * into g from public.games where id = p_game_id for update;
  if g.id is null or g.status <> 'active' then return; end if;
  if g.draw_offered_by is null or g.draw_offered_by = me then return; end if;
  if me <> g.white_id and me <> g.black_id then raise exception 'Você não participa dessa partida.'; end if;

  if p_accept then
    perform public.finalize_game(p_game_id, 'draw', 'draw_agreed');
  else
    perform set_config('chessmaui.trusted_game_write', 'true', true);
    update public.games set draw_offered_by = null where id = p_game_id;
  end if;
end;
$$;


-- ── Resultado de partidas locais (IA/Carreira/Torneio/Amigo no mesmo aparelho) ──
-- Continua auto-declarado (não existe um "adversário real" pra validar contra), mas agora
-- passa pela mesma fórmula/servidor em vez de um UPDATE livre do cliente — fecha a escrita
-- bruta de qualquer número de Elo, mesmo fora do modo online.
create or replace function public.apply_local_result(p_opponent_rating int, p_outcome text)
returns void language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  my_elo int; my_games int; my_score numeric; my_delta int;
begin
  if me is null then raise exception 'Não autenticado.'; end if;
  if p_outcome not in ('win','loss','draw') then raise exception 'Resultado inválido.'; end if;

  select elo, wins + losses into my_elo, my_games from public.profiles where id = me for update;
  if my_elo is null then return; end if;

  my_score := case p_outcome when 'win' then 1.0 when 'draw' then 0.5 else 0.0 end;
  my_delta := public.compute_elo_delta(my_elo, p_opponent_rating, coalesce(my_games,0), my_score);

  perform set_config('chessmaui.trusted_rating_write', 'true', true);
  update public.profiles set
    elo      = elo + my_delta,
    week_elo = week_elo + my_delta,
    wins     = wins   + case when p_outcome = 'win'  then 1 else 0 end,
    losses   = losses + case when p_outcome = 'loss' then 1 else 0 end
  where id = me;
end;
$$;

create or replace function public.record_tournament_win()
returns void language plpgsql security definer as $$
begin
  perform set_config('chessmaui.trusted_rating_write', 'true', true);
  update public.profiles set tournaments_won = tournaments_won + 1 where id = auth.uid();
end;
$$;


-- ── Amigo online: aceitar o código já cria a partida real (não mais 2 jogos locais) ──
alter table public.challenges add column if not exists game_id uuid references public.games(id);

-- O desafiante precisa poder ler o próprio desafio mesmo depois de aceito (pra descobrir o
-- game_id) — a policy antiga só cobria status='pending'.
create policy "Desafiante sempre lê o próprio desafio"
  on public.challenges for select
  using (challenger_id = auth.uid());

create or replace function public.accept_challenge(p_code text)
returns uuid language plpgsql security definer as $$
declare
  me uuid := auth.uid();
  c  record;
  new_game_id uuid;
  coin boolean;
  white_id_v uuid;
  black_id_v uuid;
begin
  if me is null then raise exception 'Não autenticado.'; end if;

  select * into c from public.challenges
    where code = p_code and status = 'pending' and expires_at > now()
    for update;
  if c.id is null then raise exception 'Código inválido ou expirado.'; end if;
  if c.challenger_id = me then raise exception 'Você não pode aceitar o próprio desafio.'; end if;

  coin       := random() < 0.5;
  white_id_v := case when coin then c.challenger_id else me end;
  black_id_v := case when coin then me else c.challenger_id end;
  new_game_id := gen_random_uuid();

  perform set_config('chessmaui.trusted_game_write', 'true', true);
  insert into public.games (
    id, white_id, black_id, time_minutes, moves, turn,
    white_remaining_ms, black_remaining_ms, last_move_at, status
  ) values (
    new_game_id, white_id_v, black_id_v, c.time_minutes, '{}', 'white',
    c.time_minutes::bigint * 60000, c.time_minutes::bigint * 60000, now(), 'active'
  );

  update public.challenges set status = 'accepted', game_id = new_game_id where id = c.id;

  return new_game_id;
end;
$$;


-- ── Trava de execução: só quem deveria chamar cada função pode chamá-la ──
revoke execute on function public.finalize_game(uuid, text, text) from public, anon, authenticated;

grant execute on function
  public.find_match(int),
  public.cancel_search(),
  public.submit_result_claim(uuid, text),
  public.resign_game(uuid),
  public.claim_timeout(uuid),
  public.offer_draw(uuid),
  public.respond_draw(uuid, boolean),
  public.apply_local_result(int, text),
  public.record_tournament_win(),
  public.accept_challenge(text)
to authenticated;
